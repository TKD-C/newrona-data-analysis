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

    /// <summary>Elo 점수 상수(튜닝 가능, 외부 저장: meta/eloConfig).</summary>
    public EloConfig Elo { get; set; } = new();
}

/// <summary>
/// 팀 평균 Elo 상수. 기본값은 과제 확정값(K=20, divisor=600, B=3, 미등록 상대 기본 1300).
/// Firestore meta/eloConfig 문서로 저장되며 값만 바꿔 튜닝할 수 있다.
/// </summary>
public sealed class EloConfig
{
    /// <summary>K-factor(승패 1회 최대 변동 폭).</summary>
    public int K { get; set; } = 20;

    /// <summary>expected 곡선 분모(클수록 점수차에 덜 민감). 팀 평균 Elo 방식 확정값 600.</summary>
    public double Divisor { get; set; } = 600;

    /// <summary>지표 점수 가중치(지표항 추후 구현). delta += B×(2×지표−1), 기여는 ±B로 캡 예정.</summary>
    public int B { get; set; } = 3;

    /// <summary>맞라인 상대가 미등록(PlayerId=0)일 때 간주할 점수.</summary>
    public int DefaultUnregisteredScore { get; set; } = 1300;
}

public sealed class PlayerRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string LolNickname { get; set; } = "";
    public List<string> MainLanes { get; set; } = new();
    public List<string> SubLanes { get; set; } = new();
    /// <summary>비밀: 라이엇 PUUID(관리자 전용, 슬래시 조회 비노출).</summary>
    public string Puuid { get; set; } = "";
    public int Score { get; set; } = 1000;
    /// <summary>반창고: 부라인 배정으로 누적된 빚(없으면 0). 라인 기반 팀 편성 우선권.</summary>
    public int Bandage { get; set; }
}

public sealed class MatchRecord
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; }
    public int Winner { get; set; } = 1; // 1 = Team1, 2 = Team2
    public string Note { get; set; } = "";
    /// <summary>라이엇 match-v5 ID(자동 기록). 중복 기록 방지 키. 수동 기록은 빈 문자열.</summary>
    public string RiotMatchId { get; set; } = "";
    public List<ParticipantRecord> Participants { get; set; } = new();

    /// <summary>이 경기로 적용된 내전러별 점수 증감(PlayerId → delta). 삭제 시 역적용해 점수 복구.</summary>
    public Dictionary<int, int> ScoreDeltas { get; set; } = new();

    /// <summary>이 경기로 적용된 내전러별 반창고 증감(PlayerId → delta). 삭제 시 역적용해 반창고 복구.</summary>
    public Dictionary<int, int> BandageDeltas { get; set; } = new();
}

public sealed class ParticipantRecord
{
    public int PlayerId { get; set; } // 미등록 참가자는 0
    public int Team { get; set; }     // 1 또는 2
    /// <summary>라인(탑/정글/미드/원딜/서폿). 없으면 빈 문자열.</summary>
    public string Lane { get; set; } = "";
    /// <summary>라이엇 raw teamPosition(TOP/JUNGLE/MIDDLE/BOTTOM/UTILITY). Elo 맞라인 매칭용.</summary>
    public string TeamPosition { get; set; } = "";
    /// <summary>라이엇 PUUID(자동 기록 매칭용). 비밀 정보.</summary>
    public string Puuid { get; set; } = "";
    /// <summary>미등록 참가자 표시용 이름(라이엇 닉네임). 등록 내전러는 빈 문자열(이름은 players에서 조회).</summary>
    public string Name { get; set; } = "";
}
