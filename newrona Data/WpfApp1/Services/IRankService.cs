using NewronaData.Models;

namespace NewronaData.Services;

public interface IRankService
{
    /// <summary>등급표(높은 등급부터). 점수 기준 안내에 사용.</summary>
    IReadOnlyList<ServerRank> Ranks { get; }

    /// <summary>점수에 해당하는 서버 내 등급.</summary>
    ServerRank Resolve(int score);

    /// <summary>플레이어들을 등급별로 묶어 높은 등급부터 반환.</summary>
    IReadOnlyList<RankGroup> Group(IEnumerable<Player> players);
}
