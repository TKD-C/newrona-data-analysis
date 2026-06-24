namespace NewronaData.Data;

/// <summary>스키마 생성 전담(SRP).</summary>
public sealed class DatabaseInitializer
{
    private readonly IDbConnectionFactory _factory;
    public DatabaseInitializer(IDbConnectionFactory factory) => _factory = factory;

    public void Initialize()
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Players (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT NOT NULL,
                LolNickname TEXT NOT NULL DEFAULT '',
                MainLane    TEXT NOT NULL DEFAULT '',
                SubLane     TEXT NOT NULL DEFAULT '',
                Puuid       TEXT NOT NULL DEFAULT '',
                Score       INTEGER NOT NULL DEFAULT 1000
            );

            CREATE TABLE IF NOT EXISTS Matches (
                Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                PlayedAt  TEXT NOT NULL,
                Winner    INTEGER NOT NULL,
                Note      TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS MatchPlayers (
                MatchId  INTEGER NOT NULL REFERENCES Matches(Id) ON DELETE CASCADE,
                PlayerId INTEGER NOT NULL REFERENCES Players(Id) ON DELETE CASCADE,
                Team     INTEGER NOT NULL,
                PRIMARY KEY (MatchId, PlayerId)
            );
            """;
        cmd.ExecuteNonQuery();

        // 기존 DB 마이그레이션: 새 컬럼이 없으면 추가(이미 있으면 무시).
        AddColumnIfMissing(conn, "Players", "MainLane", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(conn, "Players", "SubLane", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(conn, "Players", "Puuid", "TEXT NOT NULL DEFAULT ''");
    }

    private static void AddColumnIfMissing(System.Data.Common.DbConnection conn, string table, string column, string definition)
    {
        using var check = conn.CreateCommand();
        check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $c;";
        var p = check.CreateParameter();
        p.ParameterName = "$c";
        p.Value = column;
        check.Parameters.Add(p);
        if (Convert.ToInt32(check.ExecuteScalar()) > 0) return;

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }
}
