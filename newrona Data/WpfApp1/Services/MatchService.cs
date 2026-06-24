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

        ApplyScoring(match); // 점수 증감 계산·반영 + match.ScoreDeltas 채움(저장 전에 수행)
        _matches.Add(match);
        return match;
    }

    public Match RecordDetailed(IReadOnlyList<MatchPlayer> participants, Team winner, DateTime playedAt, string note, string riotMatchId)
    {
        var t1 = participants.Count(p => p.Team == Team.Team1);
        var t2 = participants.Count(p => p.Team == Team.Team2);
        if (t1 != TeamSize || t2 != TeamSize)
            throw new InvalidOperationException($"각 팀은 5명이어야 합니다(현재 {t1} vs {t2}).");

        var match = new Match
        {
            PlayedAt = playedAt,
            Winner = winner,
            Note = note?.Trim() ?? "",
            RiotMatchId = riotMatchId ?? "",
        };
        match.Participants.AddRange(participants);

        ApplyScoring(match); // 점수 증감 계산·반영 + match.ScoreDeltas 채움(저장 전에 수행)
        _matches.Add(match);
        return match;
    }

    public bool HasRiotMatch(string riotMatchId)
        => !string.IsNullOrEmpty(riotMatchId) && _matches.GetAll().Any(m => m.RiotMatchId == riotMatchId);

    public void Delete(int matchId) => _matches.Delete(matchId);

    private static void Validate(List<int> t1, List<int> t2)
    {
        if (t1.Count != TeamSize || t2.Count != TeamSize)
            throw new InvalidOperationException("각 팀은 5명이어야 합니다.");
        var all = t1.Concat(t2).ToList();
        if (all.Distinct().Count() != all.Count)
            throw new InvalidOperationException("중복 배정된 플레이어가 있습니다.");
    }

    /// <summary>
    /// 전략이 산출한 변동을 점수에 반영하고, 역적용(삭제 시 복구)을 위해 <see cref="Match.ScoreDeltas"/>에 기록.
    /// 경고는 <see cref="Match.ScoringWarnings"/>에 담아 호출자(슬래시 명령)가 알람으로 표시할 수 있게 한다.
    /// </summary>
    private void ApplyScoring(Match match)
    {
        var players = _players.GetAll();
        var currentScores = players.ToDictionary(p => p.Id, p => p.Score);

        var result = _scoring.CalculateDeltas(match, currentScores);
        match.ScoreDeltas = new Dictionary<int, int>(result.Deltas);
        match.ScoringWarnings = result.Warnings.ToList();

        if (result.Deltas.Count == 0) return;

        var byId = players.ToDictionary(p => p.Id);
        foreach (var (playerId, delta) in result.Deltas)
            if (delta != 0 && byId.TryGetValue(playerId, out var p))
            {
                p.Score += delta;
                _players.Update(p);
            }
    }
}
