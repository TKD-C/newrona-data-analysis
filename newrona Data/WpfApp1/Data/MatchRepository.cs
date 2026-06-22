using NewronaData.Models;

namespace NewronaData.Data;

public sealed class MatchRepository : IMatchRepository
{
    private readonly IDbConnectionFactory _factory;
    public MatchRepository(IDbConnectionFactory factory) => _factory = factory;

    public IReadOnlyList<Match> GetAll()
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT m.Id, m.PlayedAt, m.Winner, m.Note,
                   mp.PlayerId, mp.Team, p.Name
            FROM Matches m
            LEFT JOIN MatchPlayers mp ON mp.MatchId = m.Id
            LEFT JOIN Players p       ON p.Id = mp.PlayerId
            ORDER BY m.PlayedAt DESC, m.Id DESC;
            """;
        using var r = cmd.ExecuteReader();
        var map = new Dictionary<int, Match>();
        while (r.Read())
        {
            int id = r.GetInt32(0);
            if (!map.TryGetValue(id, out var match))
            {
                match = new Match
                {
                    Id = id,
                    PlayedAt = DateTime.Parse(r.GetString(1)),
                    Winner = (Team)r.GetInt32(2),
                    Note = r.GetString(3),
                };
                map[id] = match;
            }
            if (!r.IsDBNull(4))
                match.Participants.Add(new MatchPlayer
                {
                    MatchId = id,
                    PlayerId = r.GetInt32(4),
                    Team = (Team)r.GetInt32(5),
                    PlayerName = r.GetString(6),
                });
        }
        return map.Values.ToList();
    }

    public Match Add(Match match)
    {
        using var conn = _factory.Create();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO Matches (PlayedAt, Winner, Note) VALUES ($d, $w, $note);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$d", match.PlayedAt.ToString("o"));
            cmd.Parameters.AddWithValue("$w", (int)match.Winner);
            cmd.Parameters.AddWithValue("$note", match.Note);
            match.Id = Convert.ToInt32(cmd.ExecuteScalar());
        }

        foreach (var mp in match.Participants)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO MatchPlayers (MatchId, PlayerId, Team) VALUES ($m, $p, $t);";
            cmd.Parameters.AddWithValue("$m", match.Id);
            cmd.Parameters.AddWithValue("$p", mp.PlayerId);
            cmd.Parameters.AddWithValue("$t", (int)mp.Team);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return match;
    }

    public void Delete(int matchId)
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Matches WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", matchId);
        cmd.ExecuteNonQuery();
    }
}
