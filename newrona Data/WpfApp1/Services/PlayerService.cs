using NewronaData.Data;
using NewronaData.Models;

namespace NewronaData.Services;

public sealed class PlayerService : IPlayerService
{
    private readonly IPlayerRepository _repo;
    public PlayerService(IPlayerRepository repo) => _repo = repo;

    public IReadOnlyList<Player> GetPlayers() => _repo.GetAll();

    public Player Create(string name, string nickname, int score,
        IEnumerable<string>? mainLanes = null, IEnumerable<string>? subLanes = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("이름은 필수입니다.", nameof(name));
        return _repo.Add(new Player
        {
            Name = name.Trim(),
            LolNickname = nickname.Trim(),
            Score = score,
            MainLanes = NormalizeLanes(mainLanes),
            SubLanes = NormalizeLanes(subLanes),
        });
    }

    public void Update(Player player)
    {
        if (string.IsNullOrWhiteSpace(player.Name))
            throw new ArgumentException("이름은 필수입니다.", nameof(player));
        player.MainLanes = NormalizeLanes(player.MainLanes);
        player.SubLanes = NormalizeLanes(player.SubLanes);
        _repo.Update(player);
    }

    public void Delete(int playerId) => _repo.Delete(playerId);

    /// <summary>라인 정규화: 공백 제거·중복 제거 후 최대 2개로 제한.</summary>
    private static List<string> NormalizeLanes(IEnumerable<string>? lanes) => lanes is null
        ? new List<string>()
        : lanes.Where(l => !string.IsNullOrWhiteSpace(l))
               .Select(l => l.Trim())
               .Distinct()
               .Take(2)
               .ToList();
}
