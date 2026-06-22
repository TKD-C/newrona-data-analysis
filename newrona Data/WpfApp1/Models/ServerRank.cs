namespace NewronaData.Models;

/// <summary>서버 내 롤 등급 정의(이름 + 최소 점수). 데이터로 관리하여 OCP 준수.</summary>
public record ServerRank(string Name, int MinScore);

/// <summary>등급별로 묶인 플레이어 목록(정렬 표시용).</summary>
public class RankGroup
{
    public ServerRank Rank { get; init; } = new("", 0);
    public IReadOnlyList<Player> Players { get; init; } = Array.Empty<Player>();
    public string PlayersText => string.Join(", ", Players.Select(p => p.Name));
    public string Display => $"{Rank.Name} = {PlayersText}";
}
