using Microsoft.Data.Sqlite;
using NewronaData.Models;

namespace NewronaData.Data;

public sealed class PlayerRepository : IPlayerRepository
{
    private readonly IDbConnectionFactory _factory;
    public PlayerRepository(IDbConnectionFactory factory) => _factory = factory;

    public IReadOnlyList<Player> GetAll()
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        // 승/패 통계를 LEFT JOIN으로 함께 집계
        cmd.CommandText = """
            SELECT p.Id, p.Name, p.LolNickname, p.MainLane, p.SubLane, p.Puuid, p.Score,
                   COALESCE(SUM(CASE WHEN mp.Team = m.Winner THEN 1 ELSE 0 END), 0) AS Wins,
                   COALESCE(SUM(CASE WHEN mp.Team <> m.Winner THEN 1 ELSE 0 END), 0) AS Losses
            FROM Players p
            LEFT JOIN MatchPlayers mp ON mp.PlayerId = p.Id
            LEFT JOIN Matches m       ON m.Id = mp.MatchId
            GROUP BY p.Id
            ORDER BY p.Score DESC, p.Name;
            """;
        using var r = cmd.ExecuteReader();
        var list = new List<Player>();
        while (r.Read())
            list.Add(new Player
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                LolNickname = r.GetString(2),
                MainLanes = SplitLanes(r.GetString(3)),
                SubLanes = SplitLanes(r.GetString(4)),
                Puuid = r.GetString(5),
                Score = r.GetInt32(6),
                Wins = r.GetInt32(7),
                Losses = r.GetInt32(8),
            });
        return list;
    }

    public Player Add(Player p)
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Players (Name, LolNickname, MainLane, SubLane, Puuid, Score)
            VALUES ($n, $k, $ml, $sl, $pu, $s);
            SELECT last_insert_rowid();
            """;
        Bind(cmd, p);
        p.Id = Convert.ToInt32(cmd.ExecuteScalar());
        return p;
    }

    public void Update(Player p)
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Players SET Name=$n, LolNickname=$k, MainLane=$ml, SubLane=$sl, Puuid=$pu, Score=$s WHERE Id=$id;
            """;
        Bind(cmd, p);
        cmd.Parameters.AddWithValue("$id", p.Id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int playerId)
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Players WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", playerId);
        cmd.ExecuteNonQuery();
    }

    private static void Bind(SqliteCommand cmd, Player p)
    {
        cmd.Parameters.AddWithValue("$n", p.Name);
        cmd.Parameters.AddWithValue("$k", p.LolNickname);
        cmd.Parameters.AddWithValue("$ml", string.Join(",", p.MainLanes));
        cmd.Parameters.AddWithValue("$sl", string.Join(",", p.SubLanes));
        cmd.Parameters.AddWithValue("$pu", p.Puuid);
        cmd.Parameters.AddWithValue("$s", p.Score);
    }

    private static List<string> SplitLanes(string raw)
        => raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
