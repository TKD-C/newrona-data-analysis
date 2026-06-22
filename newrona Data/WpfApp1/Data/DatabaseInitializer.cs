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
                LolTier     TEXT NOT NULL DEFAULT '',
                Score       INTEGER NOT NULL DEFAULT 100
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
    }
}
