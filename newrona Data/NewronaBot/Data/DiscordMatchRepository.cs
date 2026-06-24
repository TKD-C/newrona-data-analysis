using NewronaBot.Persistence;
using NewronaData.Data;
using NewronaData.Models;

namespace NewronaBot.Data;

/// <summary><see cref="INewronaStore"/>(디스코드 채널/파이어스토어 등) 기반 경기 저장소.</summary>
public sealed class DiscordMatchRepository : IMatchRepository
{
    private readonly INewronaStore _store;
    public DiscordMatchRepository(INewronaStore store) => _store = store;

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
                RiotMatchId = m.RiotMatchId,
                ScoreDeltas = new Dictionary<int, int>(m.ScoreDeltas),
                Participants = m.Participants
                    .Select(p => new MatchPlayer
                    {
                        MatchId = m.Id,
                        PlayerId = p.PlayerId,
                        Team = (Team)p.Team,
                        Lane = p.Lane,
                        TeamPosition = p.TeamPosition,
                        Puuid = p.Puuid,
                        // 등록 내전러는 players에서 이름 조회, 미등록(PlayerId 0)은 저장된 라이엇 닉네임 사용.
                        PlayerName = nameById.TryGetValue(p.PlayerId, out var n) ? n
                            : (string.IsNullOrWhiteSpace(p.Name) ? "(미등록)" : p.Name),
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
                RiotMatchId = match.RiotMatchId,
                ScoreDeltas = new Dictionary<int, int>(match.ScoreDeltas),
                Participants = match.Participants
                    .Select(p => new ParticipantRecord
                    {
                        PlayerId = p.PlayerId,
                        Team = (int)p.Team,
                        Lane = p.Lane,
                        TeamPosition = p.TeamPosition,
                        Puuid = p.Puuid,
                        // 등록 내전러는 이름을 players에서 조회하므로 저장 불필요, 미등록만 닉네임 저장.
                        Name = p.PlayerId == 0 ? p.PlayerName : "",
                    })
                    .ToList(),
            });
        });
        return match;
    }

    public void Delete(int matchId) => _store.Mutate(db =>
    {
        var match = db.Matches.FirstOrDefault(m => m.Id == matchId);
        if (match is null) return;

        // 멱등성 방식 (a): 기록해 둔 점수 증감을 역적용해 내전러 점수를 복구한 뒤 경기 삭제.
        foreach (var (playerId, delta) in match.ScoreDeltas)
        {
            var player = db.Players.FirstOrDefault(p => p.Id == playerId);
            if (player is not null) player.Score -= delta;
        }
        db.Matches.RemoveAll(m => m.Id == matchId);
    });
}
