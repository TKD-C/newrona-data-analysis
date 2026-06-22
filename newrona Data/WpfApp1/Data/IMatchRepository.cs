using NewronaData.Models;

namespace NewronaData.Data;

public interface IMatchRepository
{
    IReadOnlyList<Match> GetAll();
    Match Add(Match match);
    void Delete(int matchId);
}
