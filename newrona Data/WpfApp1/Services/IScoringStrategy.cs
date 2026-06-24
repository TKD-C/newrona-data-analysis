using NewronaData.Models;

namespace NewronaData.Services;

/// <summary>
/// 경기 결과로부터 점수 변동을 계산하는 전략(Strategy 패턴).
/// 구현체만 교체하면 점수 시스템이 바뀐다(NoOp ↔ Elo 등).
/// </summary>
public interface IScoringStrategy
{
    /// <summary>
    /// 참가 플레이어별 점수 증감과 경고를 산출.
    /// Elo 계산에는 각 내전러의 현재 점수가 필요하므로 <paramref name="currentScores"/>(PlayerId → 현재 점수)를 받는다.
    /// </summary>
    ScoringResult CalculateDeltas(Match match, IReadOnlyDictionary<int, int> currentScores);
}

/// <summary>점수 계산 결과: 내전러별 증감(PlayerId → delta)과 경고 메시지.</summary>
public sealed class ScoringResult
{
    /// <summary>적용할 점수 증감(등록 내전러만, PlayerId &gt; 0).</summary>
    public IReadOnlyDictionary<int, int> Deltas { get; init; } = new Dictionary<int, int>();

    /// <summary>계산 중 발생한 경고(예: 맞라인 매칭 실패 → 점수 미반영). 사용자에게 알람으로 표시.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static readonly ScoringResult Empty = new();
}

/// <summary>점수 미반영(기본). Elo 등으로 교체.</summary>
public sealed class NoOpScoringStrategy : IScoringStrategy
{
    public ScoringResult CalculateDeltas(Match match, IReadOnlyDictionary<int, int> currentScores)
        => ScoringResult.Empty;
}
