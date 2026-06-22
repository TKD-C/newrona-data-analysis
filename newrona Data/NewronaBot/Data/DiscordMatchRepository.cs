using NewronaBot.Persistence;
using NewronaData.Data;
using NewronaData.Models;

namespace NewronaBot.Data;

/// <summary>디스코드 채널 JSON 저장소 기반 경기 저장소.</summary>
public sealed class DiscordMatchRepository : IMatchRepository
{
    private readonly DiscordJsonStore _store;
    public DiscordMatchRepository(DiscordJsonStore store) => _store = store;

    public IReadOnlyList<Match> GetAll() => _store.Read(db =>
    {
        var nameById = db.Players.ToDictionary(p => p.Id, p => p.Name);

        return db.Matches
            .OrderByDescending(m => m.PlayedAt)
            .ThenByDescending(m => m.Id)
            .Select(m => new Match
            {
                Id = m.Id,
                PlayedAt = m.PlayedAt,
                Winner = (Team)m.Winner,
                Note = m.Note,
                Participants = m.Participants
                    .Select(p => new MatchPlayer
                    {
                        MatchId = m.Id,
                        PlayerId = p.PlayerId,
                        Team = (Team)p.Team,
                        PlayerName = nameById.GetValueOrDefault(p.PlayerId, "(삭제됨)"),
                    })
                    .ToList(),
            })
            .ToList();
    });

    public Match Add(Match match)
    {
        _store.Mutate(db =>
        {
            match.Id = db.NextMatchId++;
            db.Matches.Add(new MatchRecord
            {
                Id = match.Id,
                PlayedAt = match.PlayedAt,
                Winner = (int)match.Winner,
                Note = match.Note,
                Participants = match.Participants
                    .Select(p => new ParticipantRecord { PlayerId = p.PlayerId, Team = (int)p.Team })
                    .ToList(),
            });
        });
        return match;
    }

    public void Delete(int matchId) => _store.Mutate(db => db.Matches.RemoveAll(m => m.Id == matchId));
}
