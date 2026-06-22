namespace NewronaData.Models;

public enum Team { Team1 = 1, Team2 = 2 }

/// <summary>5대5 내전 경기 1건.</summary>
public class Match
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; } = DateTime.Now;
    public Team Winner { get; set; } = Team.Team1;
    public string Note { get; set; } = "";
    public List<MatchPlayer> Participants { get; set; } = new();

    private string Names(Team team)
        => string.Join(", ", Participants.Where(p => p.Team == team).Select(p => p.PlayerName));

    public string Team1Names => Names(Team.Team1);
    public string Team2Names => Names(Team.Team2);
    public string WinnerText => Winner == Team.Team1 ? "1팀 승" : "2팀 승";
    public string Summary => $"[{PlayedAt:yyyy-MM-dd HH:mm}] {WinnerText}  |  1팀: {Team1Names}  vs  2팀: {Team2Names}"
                             + (string.IsNullOrWhiteSpace(Note) ? "" : $"  ({Note})");
}

/// <summary>경기-플레이어 배정(팀 소속).</summary>
public class MatchPlayer
{
    public int MatchId { get; set; }
    public int PlayerId { get; set; }
    public Team Team { get; set; }

    // 조회 편의용
    public string PlayerName { get; set; } = "";
}
