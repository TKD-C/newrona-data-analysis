using Google.Cloud.Firestore;

namespace NewronaBot.Persistence;

/// <summary>
/// 데이터를 Google Cloud Firestore(무료 Spark 플랜)에 저장하는 저장소.
///
/// 동작 방식(<see cref="DiscordJsonStore"/>와 동일한 메모리-원본 모델):
///  - 메모리에 올라온 <see cref="NewronaDatabase"/>가 실행 중 실시간 원본(single source of truth).
///  - 시작 시 Firestore의 players/matches/meta 컬렉션을 읽어 메모리로 적재.
///  - 변경이 생기면 디바운스 후 현재 메모리 상태를 Firestore에 통째로 반영(write-through).
///
/// 컬렉션 구조(문서당 1MiB 제한 회피):
///   players/{id}   → name, lolNickname, mainLanes[], subLanes[], puuid, score
///   matches/{id}   → playedAt, winner, note, participants[]
///   meta/counters  → nextPlayerId, nextMatchId
///
/// 서버 앱이므로 로그인이 아니라 "서비스 계정 키(JSON)"로 접속한다.
/// </summary>
public sealed class FirestoreStore : INewronaStore
{
    private const string PlayersCol = "players";
    private const string MatchesCol = "matches";
    private const string MetaCol = "meta";
    private const string CountersDoc = "counters";
    private const string EloConfigDoc = "eloConfig";

    private readonly FirestoreDb _firestore;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private NewronaDatabase _db = new();
    private CancellationTokenSource? _debounce;

    // 마지막으로 Firestore에 존재한다고 아는 문서 ID들. flush 시 삭제 대상을 계산하는 데 쓴다.
    private HashSet<int> _knownPlayerIds = new();
    private HashSet<int> _knownMatchIds = new();

    public FirestoreStore(FirestoreDb firestore) => _firestore = firestore;

    /// <summary>서비스 계정 키 JSON으로 FirestoreDb를 만든다.</summary>
    public static FirestoreStore Create(string projectId, string credentialsPath)
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = projectId,
            CredentialsPath = credentialsPath,
        }.Build();
        return new FirestoreStore(db);
    }

    /// <summary>Firestore에서 기존 데이터를 읽어 메모리에 적재(시작 시 1회).</summary>
    public async Task InitializeAsync()
    {
        var loaded = new NewronaDatabase();
        try
        {
            var playersSnap = await _firestore.Collection(PlayersCol).GetSnapshotAsync();
            foreach (var doc in playersSnap.Documents)
            {
                if (!int.TryParse(doc.Id, out var id)) continue;
                loaded.Players.Add(new PlayerRecord
                {
                    Id = id,
                    Name = GetString(doc, "name"),
                    LolNickname = GetString(doc, "lolNickname"),
                    MainLanes = GetStringList(doc, "mainLanes"),
                    SubLanes = GetStringList(doc, "subLanes"),
                    Puuid = GetString(doc, "puuid"),
                    Score = GetInt(doc, "score", 1000),
                });
            }

            var matchesSnap = await _firestore.Collection(MatchesCol).GetSnapshotAsync();
            foreach (var doc in matchesSnap.Documents)
            {
                if (!int.TryParse(doc.Id, out var id)) continue;
                var participants = new List<ParticipantRecord>();
                if (doc.TryGetValue<List<Dictionary<string, object>>>("participants", out var raw) && raw is not null)
                    foreach (var p in raw)
                        participants.Add(new ParticipantRecord
                        {
                            PlayerId = ToInt(p.GetValueOrDefault("playerId")),
                            Team = ToInt(p.GetValueOrDefault("team")),
                            Lane = ToStr(p.GetValueOrDefault("lane")),
                            TeamPosition = ToStr(p.GetValueOrDefault("teamPosition")),
                            Puuid = ToStr(p.GetValueOrDefault("puuid")),
                            Name = ToStr(p.GetValueOrDefault("name")),
                        });

                var scoreDeltas = new Dictionary<int, int>();
                if (doc.TryGetValue<Dictionary<string, object>>("scoreDeltas", out var rawDeltas) && rawDeltas is not null)
                    foreach (var (k, v) in rawDeltas)
                        if (int.TryParse(k, out var pid)) scoreDeltas[pid] = ToInt(v);

                loaded.Matches.Add(new MatchRecord
                {
                    Id = id,
                    PlayedAt = GetDateTime(doc, "playedAt"),
                    Winner = GetInt(doc, "winner", 1),
                    Note = GetString(doc, "note"),
                    RiotMatchId = GetString(doc, "riotMatchId"),
                    Participants = participants,
                    ScoreDeltas = scoreDeltas,
                });
            }

            // 카운터: meta/counters 문서가 있으면 사용, 없으면 기존 ID 최대치+1로 보정.
            var meta = await _firestore.Collection(MetaCol).Document(CountersDoc).GetSnapshotAsync();
            loaded.NextPlayerId = meta.Exists
                ? GetInt(meta, "nextPlayerId", 1)
                : (loaded.Players.Count == 0 ? 1 : loaded.Players.Max(p => p.Id) + 1);
            loaded.NextMatchId = meta.Exists
                ? GetInt(meta, "nextMatchId", 1)
                : (loaded.Matches.Count == 0 ? 1 : loaded.Matches.Max(m => m.Id) + 1);

            // Elo 상수: meta/eloConfig 문서가 있으면 사용, 없으면 기본값(첫 flush 때 기록됨).
            var elo = await _firestore.Collection(MetaCol).Document(EloConfigDoc).GetSnapshotAsync();
            if (elo.Exists)
                loaded.Elo = new EloConfig
                {
                    K = GetInt(elo, "k", 20),
                    Divisor = GetDouble(elo, "divisor", 4000),
                    B = GetInt(elo, "b", 3),
                    DefaultUnregisteredScore = GetInt(elo, "defaultUnregisteredScore", 1300),
                };

            lock (_lock)
            {
                _db = loaded;
                _knownPlayerIds = loaded.Players.Select(p => p.Id).ToHashSet();
                _knownMatchIds = loaded.Matches.Select(m => m.Id).ToHashSet();
            }
            Console.WriteLine($"✅ Firestore 데이터 적재: 플레이어 {loaded.Players.Count}명, 경기 {loaded.Matches.Count}건");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Firestore 적재 실패(빈 상태로 시작): {ex.Message}");
        }
    }

    /// <summary>읽기 전용 접근(잠금 하에 스냅샷 함수 실행).</summary>
    public T Read<T>(Func<NewronaDatabase, T> read)
    {
        lock (_lock) return read(_db);
    }

    /// <summary>변경 작업(잠금 하에 실행 후 저장 예약).</summary>
    public void Mutate(Action<NewronaDatabase> mutate)
    {
        lock (_lock) mutate(_db);
        RequestSave();
    }

    /// <summary>변경을 디바운스하여 Firestore에 저장(연속 변경을 합쳐 호출 수 절감).</summary>
    private void RequestSave()
    {
        lock (_lock)
        {
            _debounce?.Cancel();
            _debounce = new CancellationTokenSource();
            var token = _debounce.Token;
            _ = DelayedSaveAsync(token);
        }
    }

    private async Task DelayedSaveAsync(CancellationToken token)
    {
        try { await Task.Delay(1200, token); }
        catch (TaskCanceledException) { return; } // 더 최근 변경이 들어와 취소됨
        await FlushAsync();
    }

    /// <summary>현재 메모리 상태를 Firestore에 반영(추가/수정 + 삭제된 문서 제거).</summary>
    public async Task FlushAsync()
    {
        await _saveGate.WaitAsync();
        try
        {
            NewronaDatabase snapshot;
            HashSet<int> prevPlayers, prevMatches;
            lock (_lock)
            {
                // 직렬화 대신 메모리 참조를 그대로 읽되, 쓰기는 잠금 밖에서 한다(네트워크 I/O).
                snapshot = _db;
                prevPlayers = _knownPlayerIds;
                prevMatches = _knownMatchIds;
            }

            var batch = _firestore.StartBatch();
            var ops = 0;

            async Task CommitIfFull()
            {
                // Firestore WriteBatch는 1회 최대 500 작업 → 가득 차면 끊어서 커밋.
                if (++ops >= 450) { await batch.CommitAsync(); batch = _firestore.StartBatch(); ops = 0; }
            }

            var curPlayers = new HashSet<int>();
            foreach (var p in snapshot.Players)
            {
                curPlayers.Add(p.Id);
                batch.Set(_firestore.Collection(PlayersCol).Document(p.Id.ToString()), new Dictionary<string, object>
                {
                    ["name"] = p.Name,
                    ["lolNickname"] = p.LolNickname,
                    ["mainLanes"] = p.MainLanes,
                    ["subLanes"] = p.SubLanes,
                    ["puuid"] = p.Puuid,
                    ["score"] = p.Score,
                });
                await CommitIfFull();
            }

            var curMatches = new HashSet<int>();
            foreach (var m in snapshot.Matches)
            {
                curMatches.Add(m.Id);
                batch.Set(_firestore.Collection(MatchesCol).Document(m.Id.ToString()), new Dictionary<string, object>
                {
                    ["playedAt"] = Timestamp.FromDateTime(ToUtc(m.PlayedAt)),
                    ["winner"] = m.Winner,
                    ["note"] = m.Note,
                    ["riotMatchId"] = m.RiotMatchId,
                    ["scoreDeltas"] = m.ScoreDeltas.ToDictionary(kv => kv.Key.ToString(), kv => (object)kv.Value),
                    ["participants"] = m.Participants
                        .Select(pr => new Dictionary<string, object>
                        {
                            ["playerId"] = pr.PlayerId,
                            ["team"] = pr.Team,
                            ["lane"] = pr.Lane,
                            ["teamPosition"] = pr.TeamPosition,
                            ["puuid"] = pr.Puuid,
                            ["name"] = pr.Name,
                        })
                        .ToList(),
                });
                await CommitIfFull();
            }

            // 메모리에서 사라진 문서(삭제된 플레이어/경기) 제거.
            foreach (var goneId in prevPlayers.Except(curPlayers))
            {
                batch.Delete(_firestore.Collection(PlayersCol).Document(goneId.ToString()));
                await CommitIfFull();
            }
            foreach (var goneId in prevMatches.Except(curMatches))
            {
                batch.Delete(_firestore.Collection(MatchesCol).Document(goneId.ToString()));
                await CommitIfFull();
            }

            batch.Set(_firestore.Collection(MetaCol).Document(CountersDoc), new Dictionary<string, object>
            {
                ["nextPlayerId"] = snapshot.NextPlayerId,
                ["nextMatchId"] = snapshot.NextMatchId,
            });
            batch.Set(_firestore.Collection(MetaCol).Document(EloConfigDoc), new Dictionary<string, object>
            {
                ["k"] = snapshot.Elo.K,
                ["divisor"] = snapshot.Elo.Divisor,
                ["b"] = snapshot.Elo.B,
                ["defaultUnregisteredScore"] = snapshot.Elo.DefaultUnregisteredScore,
            });
            await batch.CommitAsync();

            lock (_lock)
            {
                _knownPlayerIds = curPlayers;
                _knownMatchIds = curMatches;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Firestore 저장 실패: {ex.Message}");
        }
        finally
        {
            _saveGate.Release();
        }
    }

    // ── 헬퍼: Firestore 문서 → 도메인 값 ─────────────────────────
    private static string GetString(DocumentSnapshot doc, string field)
        => doc.TryGetValue<string>(field, out var v) && v is not null ? v : "";

    private static int GetInt(DocumentSnapshot doc, string field, int fallback)
        => doc.TryGetValue<long>(field, out var v) ? (int)v : fallback;

    private static double GetDouble(DocumentSnapshot doc, string field, double fallback)
        => doc.TryGetValue<double>(field, out var d) ? d
         : doc.TryGetValue<long>(field, out var l) ? l
         : fallback;

    private static List<string> GetStringList(DocumentSnapshot doc, string field)
        => doc.TryGetValue<List<string>>(field, out var v) && v is not null ? v : new List<string>();

    private static DateTime GetDateTime(DocumentSnapshot doc, string field)
        => doc.TryGetValue<Timestamp>(field, out var ts) ? ts.ToDateTime().ToLocalTime() : default;

    private static int ToInt(object? value) => value is long l ? (int)l : Convert.ToInt32(value ?? 0);

    private static string ToStr(object? value) => value as string ?? "";

    // Firestore Timestamp는 UTC만 허용 → 로컬 벽시계 시간을 보존하며 UTC로 변환.
    private static DateTime ToUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dt, DateTimeKind.Local).ToUniversalTime(),
    };
}
