using NewronaBot.Persistence;
using NewronaData.Data;
using NewronaData.Models;

namespace NewronaBot.Data;

/// <summary><see cref="INewronaStore"/>(디스코드 채널/파이어스토어 등) 기반 경기 저장소.</summary>
public sealed class DiscordMatchRepository : IMatchRepository
{
    private readonly INewronaStore _store;
    public DiscordMatchRepository(INewronaStore store) => _store = store;

    public IReadOnlyList<Match> GetAll() => _store.Read(db =>
    {
        var nameById = db.Players.ToDictionary(p => p.Id, p => p.Name);

        return db.Matches
            .OrderByDescending(m => m.PlayedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => new Match
            {
                Id = m.Id,
                PlayedAt = m.PlayedAt,
                Winner = (Team)m.Winner,
                Note = m.Note,
                RiotMatchId = m.RiotMatchId,
                ScoreDeltas = new Dictionary<int, int>(m.ScoreDeltas),
                BandageDeltas = new Dictionary<int, int>(m.BandageDeltas),
                Participants = m.Participants
                    .Select(p => new MatchPlayer
                    {
                        MatchId = m.Id,
                        PlayerId = p.PlayerId,
                        Team = (Team)p.Team,
                        Lane = p.Lane,
                        TeamPosition = p.TeamPosition,
                        Puuid = p.Puuid,
                        // 등록 내전러는 players에서 이름 조회, 미등록(PlayerId 0)은 저장된 라이엇 닉네임 사용.
                        PlayerName = nameById.TryGetValue(p.PlayerId, out var n) ? n
                            : (string.IsNullOrWhiteSpace(p.Name) ? "(미등록)" : p.Name),
                    })
                    .ToList(),
            })
            .ToList();
    });

    public Match Add(Match match)
    {
        _store.Mutate(db =>
        {
            match.Id = db.NextMatchId++;

            // 반창고 정산(경기 기록 시점): 실제 플레이 라인이 주라인이면 −1, 부/오프라인이면 +1(최소 0).
            // 실제 적용된 증감만 match.BandageDeltas에 기록해 삭제 시 역적용할 수 있게 한다.
            match.BandageDeltas = AccrueBandages(db, match.Participants);

            db.Matches.Add(new MatchRecord
            {
                Id = match.Id,
                PlayedAt = match.PlayedAt,
                Winner = (int)match.Winner,
                Note = match.Note,
                RiotMatchId = match.RiotMatchId,
                ScoreDeltas = new Dictionary<int, int>(match.ScoreDeltas),
                BandageDeltas = new Dictionary<int, int>(match.BandageDeltas),
                Participants = match.Participants
                    .Select(p => new ParticipantRecord
                    {
                        PlayerId = p.PlayerId,
                        Team = (int)p.Team,
                        Lane = p.Lane,
                        TeamPosition = p.TeamPosition,
                        Puuid = p.Puuid,
                        // 등록 내전러는 이름을 players에서 조회하므로 저장 불필요, 미등록만 닉네임 저장.
                        Name = p.PlayerId == 0 ? p.PlayerName : "",
                    })
                    .ToList(),
            });
        });
        return match;
    }

    public void Delete(int matchId) => _store.Mutate(db =>
    {
        var match = db.Matches.FirstOrDefault(m => m.Id == matchId);
        if (match is null) return;

        // 멱등성 방식 (a): 기록해 둔 점수 증감을 역적용해 내전러 점수를 복구한 뒤 경기 삭제.
        foreach (var (playerId, delta) in match.ScoreDeltas)
        {
            var player = db.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is not null) player.Score -= delta;
        }
        // 반창고도 동일하게 역적용(최소 0 클램프 — Elo와 같은 근사 복구).
        foreach (var (playerId, delta) in match.BandageDeltas)
        {
            var player = db.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is not null) player.Bandage = Math.Max(0, player.Bandage - delta);
        }
        db.Matches.RemoveAll(m => m.Id == matchId);
    });

    /// <summary>표준 teamPosition(또는 추정 라인) → 한글 라인. 비거나 알 수 없으면 빈 문자열.</summary>
    private static string KoreanLane(string teamPosition) => (teamPosition ?? "").Trim().ToUpperInvariant() switch
    {
        "TOP" => "탑",
        "JUNGLE" => "정글",
        "MIDDLE" => "미드",
        "BOTTOM" => "원딜",
        "UTILITY" => "서폿",
        _ => "",
    };

    /// <summary>
    /// 경기 참가자들의 반창고를 정산한다(등록 내전러·라인 판별 가능자만).
    /// 실제 플레이 라인이 주라인이면 −1, 그 외(부/오프라인)면 +1. 최소 0으로 클램프 후 실제 적용분을 반환.
    /// </summary>
    private static Dictionary<int, int> AccrueBandages(Persistence.NewronaDatabase db, IEnumerable<MatchPlayer> participants)
    {
        var deltas = new Dictionary<int, int>();
        foreach (var mp in participants)
        {
            if (mp.PlayerId == 0) continue; // 미등록은 반창고 없음.

            var lane = KoreanLane(mp.TeamPosition);
            if (lane.Length == 0) continue; // 라인 정보 없으면(수동 기록 등) 정산 불가.

            var rec = db.Players.FirstOrDefault(p => p.Id == mp.PlayerId);
            if (rec is null || rec.MainLanes.Count == 0) continue; // 주라인 데이터 없으면 판별 불가.

            var raw = rec.MainLanes.Contains(lane) ? -1 : +1; // 주라인이면 빚 갚음, 아니면 빚 쌓임.
            var newVal = Math.Max(0, rec.Bandage + raw);
            var applied = newVal - rec.Bandage;
            rec.Bandage = newVal;
            if (applied != 0) deltas[mp.PlayerId] = applied;
        }
        return deltas;
    }
}
