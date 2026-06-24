namespace NewronaData.Models;

/// <summary>내전러(내전 참가자). 서버 내 등급은 Score로부터 파생된다.</summary>
public class Player
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LolNickname { get; set; } = "";
    public int Score { get; set; } = 1000;

    /// <summary>주 라인(최대 2개). 예: 탑, 미드.</summary>
    public List<string> MainLanes { get; set; } = new();
    /// <summary>부 라인(최대 2개).</summary>
    public List<string> SubLanes { get; set; } = new();

    /// <summary>
    /// 반창고(:adhesive_bandage:): 부라인으로 배정돼 누적된 '빚'.
    /// 라인 기반 팀 편성(/팀짜주기라인) 시 많을수록 주라인 우선권을 갖는다.
    /// 경기 기록 시 실제 플레이 라인이 부/오프라인이면 +1, 주라인이면 −1(최소 0)로 정산된다.
    /// </summary>
    public int Bandage { get; set; }

    /// <summary>반창고 표시(예: <c>:adhesive_bandage:×2</c>). 0이면 빈 문자열.</summary>
    public string BandageText => Bandage > 0 ? $":adhesive_bandage:×{Bandage}" : "";

    /// <summary>
    /// 비밀 정보: 라이엇 PUUID(나중에 match id 조회용).
    /// 슬래시 명령으로 조회되지 않으며, 서버 관리자만 설정/조회한다.
    /// </summary>
    public string Puuid { get; set; } = "";

    /// <summary>경기 통계(조회 시 채워짐, 저장 대상 아님).</summary>
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Games => Wins + Losses;
    public double WinRate => Games == 0 ? 0 : (double)Wins / Games;

    public string MainLanesText => string.Join(", ", MainLanes);
    public string SubLanesText => string.Join(", ", SubLanes);
}
