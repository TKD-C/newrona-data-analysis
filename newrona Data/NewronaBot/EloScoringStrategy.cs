using NewronaBot.Persistence;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaBot;

/// <summary>
/// 팀 평균 Elo 점수 전략.
/// <code>
/// expected_T = 1 / (1 + 10^((상대팀평균 − 우리팀평균) / divisor))
/// delta_T    = K × (팀승패(1/0) − expected_T)   [ + B × (2 × 지표 − 1) : 지표항 추후 구현 ]
/// </code>
/// - 두 팀의 <b>평균 Elo</b>를 비교해 예상 승률을 구하고, <b>한 팀의 모든 등록 내전러에게 같은 delta</b>를 적용한다.
///   (맞라인 1v1 비교가 아니므로 teamPosition·라인 매칭이 필요 없다.)
/// - 평균에는 미등록 참가자(PlayerId=0)도 <see cref="EloConfig.DefaultUnregisteredScore"/>(기본 1300)로 포함한다.
/// - 등록 내전러(PlayerId&gt;0)에게만 delta를 산출(미등록은 점수 없음).
/// - 반올림은 <b>내림</b>(<see cref="Math.Floor(double)"/>).
/// 상수(K/divisor/B/기본 미등록 점수)는 <see cref="NewronaDatabase.Elo"/>(Firestore meta/eloConfig)에서 읽는다.
/// </summary>
public sealed class EloScoringStrategy : IScoringStrategy
{
    private readonly INewronaStore _store;

    public EloScoringStrategy(INewronaStore store) => _store = store;

    public ScoringResult CalculateDeltas(Match match, IReadOnlyDictionary<int, int> currentScores)
    {
        var cfg = _store.Read(db => db.Elo);

        var t1 = match.Participants.Where(p => p.Team == Team.Team1).ToList();
        var t2 = match.Participants.Where(p => p.Team == Team.Team2).ToList();

        if (t1.Count == 0 || t2.Count == 0)
            return new ScoringResult
            {
                Warnings = new[] { "⚠️ 양 팀 참가자가 모두 필요해 점수를 반영하지 않았습니다." },
            };

        double avg1 = TeamAverage(t1, currentScores, cfg);
        double avg2 = TeamAverage(t2, currentScores, cfg);

        // 팀 단위 expected(제로섬: expected1 + expected2 = 1).
        double expected1 = 1.0 / (1.0 + Math.Pow(10, (avg2 - avg1) / cfg.Divisor));
        double expected2 = 1.0 - expected1;
        double result1 = match.Winner == Team.Team1 ? 1.0 : 0.0;
        double result2 = 1.0 - result1;

        // 팀별 delta = K×(승패 − expected). 같은 팀이면 전원 동일 적용. 지표항(B)은 추후 구현.
        int delta1 = (int)Math.Floor(cfg.K * (result1 - expected1));
        int delta2 = (int)Math.Floor(cfg.K * (result2 - expected2));

        var deltas = new Dictionary<int, int>();
        foreach (var p in t1) if (p.PlayerId != 0) deltas[p.PlayerId] = delta1;
        foreach (var p in t2) if (p.PlayerId != 0) deltas[p.PlayerId] = delta2;

        return new ScoringResult { Deltas = deltas };
    }

    /// <summary>한 팀의 평균 점수. 미등록 참가자는 기본 미등록 점수로 간주.</summary>
    private static double TeamAverage(List<MatchPlayer> team, IReadOnlyDictionary<int, int> scores, EloConfig cfg)
        => team.Average(p => p.PlayerId != 0 && scores.TryGetValue(p.PlayerId, out var s)
            ? s
            : cfg.DefaultUnregisteredScore);
}
