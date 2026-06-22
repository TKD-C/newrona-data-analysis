using NewronaData.Data;
using NewronaData.Models;

namespace NewronaData.Services;

public sealed class PlayerService : IPlayerService
{
    private readonly IPlayerRepository _repo;
    public PlayerService(IPlayerRepository repo) => _repo = repo;

    public IReadOnlyList<Player> GetPlayers() => _repo.GetAll();

    public Player Create(string name, string nickname, string tier, int score)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("이름은 필수입니다.", nameof(name));
        return _repo.Add(new Player
        {
            Name = name.Trim(),
            LolNickname = nickname.Trim(),
            LolTier = tier.Trim(),
            Score = score,
        });
    }

    public void Update(Player player)
    {
        if (string.IsNullOrWhiteSpace(player.Name))
            throw new ArgumentException("이름은 필수입니다.", nameof(player));
        _repo.Update(player);
    }

    public void Delete(int playerId) => _repo.Delete(playerId);
}
