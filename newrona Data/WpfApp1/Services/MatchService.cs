using NewronaData.Data;
using NewronaData.Models;

namespace NewronaData.Services;

public sealed class MatchService : IMatchService
{
    private const int TeamSize = 5;

    private readonly IMatchRepository _matches;
    private readonly IPlayerRepository _players;
    private readonly IScoringStrategy _scoring;

    public MatchService(IMatchRepository matches, IPlayerRepository players, IScoringStrategy scoring)
    {
        _matches = matches;
        _players = players;
        _scoring = scoring;
    }

    public IReadOnlyList<Match> GetMatches() => _matches.GetAll();

    public Match Record(IEnumerable<int> team1, IEnumerable<int> team2, Team winner, DateTime playedAt, string note)
    {
        var t1 = team1.ToList();
        var t2 = team2.ToList();
        Validate(t1, t2);

        var match = new Match { PlayedAt = playedAt, Winner = winner, Note = note?.Trim() ?? "" };
        match.Participants.AddRange(t1.Select(id => new MatchPlayer { PlayerId = id, Team = Team.Team1 }));
        match.Participants.AddRange(t2.Select(id => new MatchPlayer { PlayerId = id, Team = Team.Team2 }));

        _matches.Add(match);
        ApplyScoring(match);
        return match;
    }

    public void Delete(int matchId) => _matches.Delete(matchId);

    private static void Validate(List<int> t1, List<int> t2)
    {
        if (t1.Count != TeamSize || t2.Count != TeamSize)
            throw new InvalidOperationException("각 팀은 5명이어야 합니다.");
        var all = t1.Concat(t2).ToList();
        if (all.Distinct().Count() != all.Count)
            throw new InvalidOperationException("중복 배정된 플레이어가 있습니다.");
    }

    /// <summary>전략이 산출한 변동을 점수에 반영(현재는 NoOp).</summary>
    private void ApplyScoring(Match match)
    {
        var deltas = _scoring.CalculateDeltas(match);
        if (deltas.Count == 0) return;

        var byId = _players.GetAll().ToDictionary(p => p.Id);
        foreach (var (playerId, delta) in deltas)
            if (byId.TryGetValue(playerId, out var p))
            {
                p.Score += delta;
                _players.Update(p);
            }
    }
}
