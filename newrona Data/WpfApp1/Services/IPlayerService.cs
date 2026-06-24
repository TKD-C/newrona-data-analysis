using NewronaData.Models;

namespace NewronaData.Services;

public interface IPlayerService
{
    IReadOnlyList<Player> GetPlayers();
    Player Create(string name, string nickname, int score,
        IEnumerable<string>? mainLanes = null, IEnumerable<string>? subLanes = null);
    void Update(Player player);
    void Delete(int playerId);
}
