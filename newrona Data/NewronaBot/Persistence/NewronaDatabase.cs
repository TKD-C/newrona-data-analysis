namespace NewronaBot.Persistence;

/// <summary>
/// 디스코드 채널에 JSON으로 직렬화되는 데이터 루트.
/// 도메인 모델(Player/Match)에는 계산 속성(승/패/승률 등)이 있어 직렬화 대상이 아니므로,
/// 저장 전용 DTO를 따로 둬서 저장 포맷을 안정적으로 유지한다.
/// </summary>
public sealed class NewronaDatabase
{
    public List<PlayerRecord> Players { get; set; } = new();
    public List<MatchRecord> Matches { get; set; } = new();
    public int NextPlayerId { get; set; } = 1;
    public int NextMatchId { get; set; } = 1;
}

public sealed class PlayerRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LolNickname { get; set; } = "";
    public string LolTier { get; set; } = "";
    public int Score { get; set; } = 100;
}

public sealed class MatchRecord
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; }
    public int Winner { get; set; } = 1; // 1 = Team1, 2 = Team2
    public string Note { get; set; } = "";
    public List<ParticipantRecord> Participants { get; set; } = new();
}

public sealed class ParticipantRecord
{
    public int PlayerId { get; set; }
    public int Team { get; set; } // 1 또는 2
}
