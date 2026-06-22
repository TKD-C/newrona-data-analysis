namespace NewronaData.Models;

/// <summary>내전 참가 플레이어. 서버 내 등급은 Score로부터 파생된다.</summary>
public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LolNickname { get; set; } = "";
    public string LolTier { get; set; } = "";
    public int Score { get; set; } = 100;

    /// <summary>경기 통계(조회 시 채워짐, 저장 대상 아님).</summary>
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Games => Wins + Losses;
    public double WinRate => Games == 0 ? 0 : (double)Wins / Games;
}
