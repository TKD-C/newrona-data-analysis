using NewronaData.Models;

namespace NewronaData.Services;

public interface IMatchService
{
    IReadOnlyList<Match> GetMatches();
    Match Record(IEnumerable<int> team1, IEnumerable<int> team2, Team winner, DateTime playedAt, string note);
    void Delete(int matchId);
}
