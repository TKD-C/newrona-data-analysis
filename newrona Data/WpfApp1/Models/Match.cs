namespace NewronaData.Models;

public enum Team { Team1 = 1, Team2 = 2 }

/// <summary>5대5 내전 경기 1건.</summary>
public class Match
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; } = DateTime.Now;
    public Team Winner { get; set; } = Team.Team1;
    public string Note { get; set; } = "";

    /// <summary>라이엇 match-v5 경기 ID. 수동 기록은 빈 문자열, 자동 기록(내전기록하기)은 채워짐 → 중복 기록 방지 키.</summary>
    public string RiotMatchId { get; set; } = "";

    public List<MatchPlayer> Participants { get; set; } = new();

    /// <summary>
    /// 이 경기로 적용된 내전러별 점수 증감(PlayerId → delta). 저장 대상.
    /// 경기 삭제 시 이 값을 역적용해 점수를 복구한다(Elo 멱등성 방식 (a)).
    /// 점수 미반영(NoOp/수동/라인 매칭 실패) 경기는 비어 있음.
    /// </summary>
    public Dictionary<int, int> ScoreDeltas { get; set; } = new();

    /// <summary>
    /// 이 경기로 적용된 내전러별 반창고 증감(PlayerId → delta). 저장 대상.
    /// 실제 플레이 라인이 주라인이면 −1, 부/오프라인이면 +1(최소 0 클램프 후 실제 적용분 기록).
    /// 경기 삭제 시 이 값을 역적용해 반창고를 복구한다. 라인 정보 없는(수동) 경기는 비어 있음.
    /// </summary>
    public Dictionary<int, int> BandageDeltas { get; set; } = new();

    /// <summary>점수 계산 중 발생한 경고(예: 맞라인 매칭 실패). 일시적 정보 — 저장하지 않음.</summary>
    public List<string> ScoringWarnings { get; set; } = new();

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

    /// <summary>등록된 내전러의 ID. 미등록 참가자(자동 기록 시)는 0.</summary>
    public int PlayerId { get; set; }
    public Team Team { get; set; }

    /// <summary>라인(탑/정글/미드/원딜/서폿). 자동 기록 시 채워짐, 수동 기록은 빈 문자열.</summary>
    public string Lane { get; set; } = "";

    /// <summary>
    /// 라이엇 raw teamPosition(TOP/JUNGLE/MIDDLE/BOTTOM/UTILITY). 자동 기록 시 채워짐.
    /// Elo 맞라인 1v1 매칭에 사용 — 정상 5v5는 전원 정확히 채워지므로 <see cref="Lane"/>(추정값)보다 신뢰.
    /// </summary>
    public string TeamPosition { get; set; } = "";

    /// <summary>라이엇 PUUID(자동 기록 시 매칭/표시용). 비밀 정보이므로 외부 노출 금지.</summary>
    public string Puuid { get; set; } = "";

    // 조회 편의용
    public string PlayerName { get; set; } = "";
}
