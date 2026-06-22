using NewronaData.Models;

namespace NewronaData.Services;

/// <summary>
/// 경기 결과로부터 점수 변동을 계산하는 전략(Strategy 패턴).
/// 점수 시스템은 추후 구현 예정 — 구현체만 교체하면 됨.
/// </summary>
public interface IScoringStrategy
{
    /// <summary>참가 플레이어별 점수 증감을 반환(PlayerId → delta).</summary>
    IReadOnlyDictionary<int, int> CalculateDeltas(Match match);
}

/// <summary>점수 미반영(기본). 추후 Elo 등으로 교체.</summary>
public sealed class NoOpScoringStrategy : IScoringStrategy
{
    public IReadOnlyDictionary<int, int> CalculateDeltas(Match match)
        => new Dictionary<int, int>();
}
