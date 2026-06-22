using NewronaData.Models;

namespace NewronaData.Services;

/// <summary>
/// 점수→등급 변환. 등급표는 주입 가능하여 확장/변경에 열려 있음(OCP).
/// </summary>
public sealed class RankService : IRankService
{
    private readonly IReadOnlyList<ServerRank> _ranks; // 내림차순 정렬 보장

    /// <summary>과제 기본 등급표.</summary>
    public static readonly IReadOnlyList<ServerRank> Default = new[]
    {
        new ServerRank("OP",     200),
        new ServerRank("★★★",  190),
        new ServerRank("★★",    180),
        new ServerRank("★",      170),
        new ServerRank("칼칼칼", 160),
        new ServerRank("칼칼",   150),
        new ServerRank("칼",     140),
        new ServerRank("번번번", 130),
        new ServerRank("번번",   120),
        new ServerRank("번",     110),
        new ServerRank("고양이", 100),
    };

    public RankService(IReadOnlyList<ServerRank>? ranks = null)
        => _ranks = (ranks ?? Default).OrderByDescending(r => r.MinScore).ToList();

    public ServerRank Resolve(int score)
        => _ranks.FirstOrDefault(r => score >= r.MinScore) ?? _ranks[^1];

    public IReadOnlyList<RankGroup> Group(IEnumerable<Player> players)
    {
        var byRank = players
            .GroupBy(p => Resolve(p.Score))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.Score).ToList());

        return _ranks
            .Where(byRank.ContainsKey)
            .Select(r => new RankGroup { Rank = r, Players = byRank[r] })
            .ToList();
    }
}
