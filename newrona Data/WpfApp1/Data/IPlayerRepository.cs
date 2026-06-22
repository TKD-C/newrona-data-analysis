using NewronaData.Models;

namespace NewronaData.Data;

public interface IPlayerRepository
{
    IReadOnlyList<Player> GetAll();
    Player Add(Player player);
    void Update(Player player);
    void Delete(int playerId);
}
