using NewronaData.Models;

namespace NewronaData.Services;

public interface IPlayerService
{
    IReadOnlyList<Player> GetPlayers();
    Player Create(string name, string nickname, string tier, int score);
    void Update(Player player);
    void Delete(int playerId);
}
