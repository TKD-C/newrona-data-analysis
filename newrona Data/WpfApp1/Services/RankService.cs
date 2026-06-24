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
        new ServerRank(":dragon_face:",                                   2000),
        new ServerRank(":star::star::star:",                              1900),
        new ServerRank(":star::star:",                                    1800),
        new ServerRank(":star:",                                          1700),
        new ServerRank(":crossed_swords::crossed_swords::crossed_swords:", 1600),
        new ServerRank(":crossed_swords::crossed_swords:",                1500),
        new ServerRank(":crossed_swords:",                                1400),
        new ServerRank(":zap::zap::zap:",                                 1300),
        new ServerRank(":zap::zap:",                                      1200),
        new ServerRank(":zap:",                                           1100),
        new ServerRank(":cat:",                                           1000),
    };

    public RankService(IReadOnlyList<ServerRank>? ranks = null)
        => _ranks = (ranks ?? Default).OrderByDescending(r => r.MinScore).ToList();

    public IReadOnlyList<ServerRank> Ranks => _ranks;

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
