using NewronaData.Models;

namespace NewronaData.Services;

public interface IMatchService
{
    IReadOnlyList<Match> GetMatches();
    Match Record(IEnumerable<int> team1, IEnumerable<int> team2, Team winner, DateTime playedAt, string note);

    /// <summary>이미 만들어진 참가자 구성으로 경기 1건 기록(라이엇 자동 기록용). 각 팀 5명 검증.</summary>
    Match RecordDetailed(IReadOnlyList<MatchPlayer> participants, Team winner, DateTime playedAt, string note, string riotMatchId);

    /// <summary>해당 라이엇 match-v5 ID가 이미 기록되어 있는지(중복 방지).</summary>
    bool HasRiotMatch(string riotMatchId);

    void Delete(int matchId);
}
