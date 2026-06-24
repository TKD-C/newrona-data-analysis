using NewronaBot.Persistence;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaBot;

/// <summary>
/// 맞라인 1v1 Elo 점수 전략.
/// <code>
/// expected = 1 / (1 + 10^((맞라인상대 − 나) / divisor))
/// delta    = K × (팀승패(1/0) − expected)   [ + B × (2 × 지표 − 1) : 지표항 추후 구현 ]
/// </code>
/// - 라인 매칭은 <see cref="MatchPlayer.TeamPosition"/>(teamPosition)을 신뢰 — 정상 5v5는 10명 전원 정확히 채워짐.
///   양 팀 모두 TOP/JUNGLE/MIDDLE/BOTTOM/UTILITY가 1명씩 잡히지 않으면 <b>점수 미반영 + 경고</b>(부분 반영 금지).
/// - 맞라인 상대가 미등록(PlayerId=0)이면 <see cref="EloConfig.DefaultUnregisteredScore"/>(기본 1300)로 간주.
/// - 등록 내전러(PlayerId&gt;0)에게만 delta를 산출(미등록은 점수 없음).
/// - 반올림은 <b>내림</b>(<see cref="Math.Floor(double)"/>).
/// 상수(K/divisor/B/기본 미등록 점수)는 <see cref="NewronaDatabase.Elo"/>(Firestore meta/eloConfig)에서 읽는다.
/// </summary>
public sealed class EloScoringStrategy : IScoringStrategy
{
    // 라이엇 표준 포지션 코드(맞라인 매칭 키).
    private static readonly string[] Lanes = { "TOP", "JUNGLE", "MIDDLE", "BOTTOM", "UTILITY" };
    private static readonly HashSet<string> LaneSet = new(Lanes);

    private readonly INewronaStore _store;

    public EloScoringStrategy(INewronaStore store) => _store = store;

    public ScoringResult CalculateDeltas(Match match, IReadOnlyDictionary<int, int> currentScores)
    {
        var cfg = _store.Read(db => db.Elo);

        // teamPosition 기준으로 각 팀을 라인별로 매핑(라인당 정확히 1명일 때만 유효).
        var t1 = MapByLane(match.Participants.Where(p => p.Team == Team.Team1));
        var t2 = MapByLane(match.Participants.Where(p => p.Team == Team.Team2));

        // 어느 한 라인이라도 양 팀에서 깔끔히 잡히지 않으면 → 알람 후 점수 미반영(부분 반영 금지).
        var unmatched = Lanes.Where(l => !t1.ContainsKey(l) || !t2.ContainsKey(l)).ToList();
        if (unmatched.Count > 0)
            return new ScoringResult
            {
                Warnings = new[]
                {
                    $"⚠️ 맞라인을 정할 수 없어 점수를 반영하지 않았습니다(teamPosition 누락/중복: " +
                    $"{string.Join(", ", unmatched.Select(KoreanLane))}). 리메이크·비정상 게임일 수 있습니다.",
                },
            };

        var deltas = new Dictionary<int, int>();
        foreach (var lane in Lanes)
        {
            ScoreOne(t1[lane], t2[lane], match.Winner, cfg, currentScores, deltas);
            ScoreOne(t2[lane], t1[lane], match.Winner, cfg, currentScores, deltas);
        }
        return new ScoringResult { Deltas = deltas };
    }

    /// <summary><paramref name="me"/>(등록 내전러)의 맞라인 1v1 Elo 증감을 계산해 <paramref name="deltas"/>에 기록.</summary>
    private static void ScoreOne(MatchPlayer me, MatchPlayer opp, Team winner, EloConfig cfg,
        IReadOnlyDictionary<int, int> scores, Dictionary<int, int> deltas)
    {
        if (me.PlayerId == 0) return; // 미등록 본인은 점수 없음.

        var myScore = scores.GetValueOrDefault(me.PlayerId, cfg.DefaultUnregisteredScore);
        var oppScore = opp.PlayerId != 0 && scores.TryGetValue(opp.PlayerId, out var s)
            ? s
            : cfg.DefaultUnregisteredScore; // 미등록 상대는 기본 점수로 간주.

        var expected = 1.0 / (1.0 + Math.Pow(10, (oppScore - myScore) / cfg.Divisor));
        var result = winner == me.Team ? 1.0 : 0.0;

        // delta = K×(승패 − expected). 지표항(+ B×(2×지표−1))은 추후 구현 — 현재 미반영.
        var raw = cfg.K * (result - expected);
        deltas[me.PlayerId] = (int)Math.Floor(raw);
    }

    /// <summary>한 팀을 teamPosition(표준 코드)별로 매핑. 라인당 정확히 1명·유효 코드일 때만 포함.</summary>
    private static Dictionary<string, MatchPlayer> MapByLane(IEnumerable<MatchPlayer> team)
        => team
            .Select(p => (lane: Canon(p.TeamPosition), p))
            .Where(x => LaneSet.Contains(x.lane))
            .GroupBy(x => x.lane)
            .Where(g => g.Count() == 1) // 중복 라인은 모호 → 제외(→ unmatched로 잡혀 경고)
            .ToDictionary(g => g.Key, g => g.First().p);

    private static string Canon(string? teamPosition) => (teamPosition ?? "").Trim().ToUpperInvariant();

    private static string KoreanLane(string lane) => lane switch
    {
        "TOP" => "탑",
        "JUNGLE" => "정글",
        "MIDDLE" => "미드",
        "BOTTOM" => "원딜",
        "UTILITY" => "서폿",
        _ => lane,
    };
}
