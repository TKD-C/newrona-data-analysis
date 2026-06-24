# 뉴로나 내전 기록기 (Newrona Data)

소규모 디스코드 서버용 **리그 오브 레전드 5대5 내전 경기 기록 데스크톱 앱**.

## 스택
- C# / WPF (.NET 9, `net9.0-windows`)
- SQLite (`Microsoft.Data.Sqlite`)
- 패턴: MVVM, Repository, Service Layer, Strategy(점수), Composition Root(DI)

## 실행
```
dotnet run --project "WpfApp1/WpfApp1.csproj"
```
실행 시 출력 폴더에 `newrona.db` 자동 생성·초기화.

## 기능
1. **내전러**(=플레이어) 추가/수정/삭제 + 목록(주/부 라인·승·패·승률 집계 표시) — `내전러` 탭
2. **5대5 경기** 수동 입력(팀별 5명 선택, 승리 팀, 일시, 비고) 및 기록 목록/삭제 — `경기 기록` 탭
3. **서버 내 등급** 점수 기준 정렬 표시 (예: `:dragon_face: = 서버원1, 서버원2`) — `서버 등급` 탭

## 서버 내 롤 등급표 (점수 기준)
:dragon_face:≥2000, :star::star::star:≥1900, :star::star:≥1800, :star:≥1700,
:crossed_swords:×3≥1600, :crossed_swords:×2≥1500, :crossed_swords:≥1400,
:zap:×3≥1300, :zap:×2≥1200, :zap:≥1100, :cat:≥1000
- 등급명은 디스코드 이모지 단축코드(`:star:`/`:crossed_swords:`/`:zap:`/`:cat:`) 사용.
- `Services/RankService.cs`의 `Default` 배열로 관리(주입 가능 → OCP). 신규 내전러 기본 점수 1000(=:cat:).

## 폴더 구조 (WpfApp1/)
```
Models/      Player, Match/MatchPlayer/Team, ServerRank/RankGroup
Data/        IDbConnectionFactory, SqliteConnectionFactory, DatabaseInitializer,
             I/PlayerRepository, I/MatchRepository
Services/    I/PlayerService, I/MatchService, I/RankService,
             IScoringStrategy + NoOpScoringStrategy
ViewModels/  ViewModelBase, RelayCommand, Main/PlayerList/Match/RankViewModel
Views/       PlayerView, MatchView, RankView (UserControl)
App.xaml.cs  합성 루트(의존성 조립)
MainWindow   TabControl 3탭
```

## 설계 노트 (SOLID/확장성)
- **점수 시스템**: `IScoringStrategy`로 추상화, 기본 `NoOpScoringStrategy` 주입.
  Elo 구현체(`EloScoringStrategy`)로 교체하면 경기 저장 시 자동 점수 반영(`MatchService.ApplyScoring`).
  설계 확정·구현 예정 — 아래 **점수(Elo) 시스템** 섹션 참조.
- 등급은 점수에서 파생(`RankService.Resolve`). DB에는 점수만 저장.
- 저장소 인터페이스(DIP)로 SQLite 외 다른 저장소로 교체 가능.
  → 디스코드 봇(`NewronaBot`)이 바로 이 점을 활용: `IPlayerRepository`/`IMatchRepository`만
     디스코드 채널 JSON 구현으로 교체하고 `Services` 계층 전체를 복제 없이 재사용.
- 등급표 외부 주입 가능(OCP).

## 디스코드 봇 (NewronaBot/)
- 콘솔 앱(`net9.0`) + Discord.Net. WPF 앱과 **데이터/실행 분리**(SQLite 미사용).
- 도메인/서비스 코드는 csproj `<Compile Include>` 링크로 **WpfApp1에서 공유**(복제 없음).
- 저장: **Google Cloud Firestore**(무료 Spark) — `Persistence/FirestoreStore`.
  저장소는 `INewronaStore`(Read/Mutate/Initialize/Flush)로 추상화 → 백엔드 교체 가능
  (기존 `DiscordJsonStore`도 같은 인터페이스 구현체로 남아 있음).
  메모리(`NewronaDatabase`)가 실시간 원본 → 변경 시 디바운스 후 write-through, 시작 시 적재.
  컬렉션 분리: `players/{id}`(name, lolNickname, mainLanes[], subLanes[], puuid, score, bandage),
  `matches/{id}`(playedAt, winner, note, riotMatchId, scoreDeltas{playerId→delta}, bandageDeltas{playerId→delta},
  participants[{playerId, team, lane, teamPosition, puuid, name}]), `meta/counters`, `meta/eloConfig`(k, divisor, b, defaultUnregisteredScore).
  서비스 계정 키 JSON(`firebase-key.json`)로 접속 — `.gitignore` 등록(비밀).
- 명령어: `Commands/`의 슬래시 모듈(내전러/경기/등급/내전기록하기/팀짜주기). 설정·실행법은 `NewronaBot/README.md`.
- **`/내전러목록`**(`Commands/PlayerCommands.cs`): 등록 내전러 목록(ephemeral). 한 줄에 **이름 · 점수 · 등급 · 주 라인 · N승 M패(승률%)**.
  롤아이디(닉네임)·부 라인·반창고는 표시하지 않음(주 라인은 `탑` 식으로 라벨 없이). 주 라인 없으면 "없음".
- **`/내전러정보 (이름)`**(`Commands/PlayerCommands.cs`): 내전러 한 명의 현재 정보를 카드(Embed)로 표시. 역할 제한 없음·누구나 사용,
  결과는 ephemeral(본인만). 표시: `:ninja: 이름 (닉네임#태그)` / 점수(mmr) `:fleur_de_lis:` / 등급(`RankService.Resolve`) /
  주 라인 / 부 라인 / `:adhesive_bandage:×N`(라벨 없이 그대로). 라인·닉네임 없으면 "없음"/생략, 이름 못 찾으면 경고. 단일 대상 확인용이라
  승/패·승률은 제외(그건 `/내전러목록`). 읽기 전용(저장/모델 변경 없음).
- **팀 자동 편성(`Commands/TeamCommands.cs`, `[RequireRole("내전관리자")]`)**: 이름 10명을 공백/쉼표로 입력받아
  (`Resolve` — 미등록·중복·인원수(정확히 10) 검증) 5대5 편성. 결과는 팀별 멤버·점수, 팀 평균 Elo, 평균차 임베드.
  - **`/팀짜주기뱀`**: (라인 무시, `Player.Score`만) 점수 내림차순 후 스네이크 `1,2,2,1,1,2,2,1,1,2`(1팀=1·4·5·8·9위, 2팀=2·3·6·7·10위).
    첫 사람 50% 코인플립은 멤버 구성 불변·팀 라벨만 무작위 반전(연출).
  - **`/팀짜주기랜덤`**: (라인 무시) 5:5 조합 126개(0번을 team1 고정해 라벨 대칭 중복 제거) 전수 생성 → 평균차 ≤40(`BalanceThreshold`)만
    추려 `Random`으로 1개 추첨. 유효 조합 0개면 가장 평균차 작은 조합 + 경고.
  - **`/팀짜주기라인`**(라인 기반): 주라인 위주로 5대5 짜기. 126개 split × 각 팀 5라인 배정 전수 탐색(`Perms5`=5!).
    **사전식 우선순위**로 최적 편성 선택:
    ① 주라인 커버 최대화(`NonMain` 최소) → ② 강제 배정 최소(`Forced`, 주·부 모두 불가) →
    ③ **반창고 많은 사람이 주라인**(`BandageOff`=주라인 못 받은 사람의 반창고 합 최소) → ④ **가상** 평균 Elo 균형.
    - ①②③은 팀별 독립이라 각 팀에서 사전식 최적(`BestPlan`)을 따로 구한 뒤, ④ 균형은 두 팀 최적 후보들의 가상점수합 차로 선택.
    - **가상 점수**(균형 계산 전용, 실제 `Score`는 불변): 주라인=`Score`, 부라인=`Score−50`(`SubPenalty`=l),
      강제(주·부 모두 불가)=`Score−150`(`ForcedPenalty`=3l).
    - 주라인 2개인 사람은 비는 라인으로 자연 배정(커버 최대화의 결과). 반창고 변경 없음(읽기 전용).
- **PUUID**는 비밀 정보 — 일반 명령/목록 비노출, `[RequireUserPermission(Administrator)]` + ephemeral
  로 서버 관리자만 `/내전러puuid설정`(PUUID 직접 입력)·`/내전러puuid조회`·`/내전러puuid연결`(롤 ID `AAA#BB`로
  account-v1 조회해 PUUID+닉네임 저장, 조회 실패 시 기존 값 보존 — `내전러추가`의 PUUID 연결만 단독 실행하는 백업용) 가능.
- **등급 명령(분할됨)**: `/등급기준표`(점수→등급 기준표, `RankService.Ranks`)와 `/서버내등급`(서버 내 내전러 등급)으로 분리.
- **도움말(`Commands/HelpCommands.cs`, ephemeral)**: `/도움말`(누구나 — 내전러 보기·등급 보기·기타)과 `/관리자도움말`(`[RequireRole("내전관리자")]` — 내전러/PUUID/경기/팀 편성 관리 명령 모음). 관리자도움말은 자주 쓰는 `/내전기록하기`·`/팀짜주기라인`에 `*` 표시 + 맨 아래 `ℹ️ 안내` 필드로 `*` 범례(주로 사용되는 명령) 안내. `/내전기록하기` 설명은 "참여자 1명 지정 → 그 사람 최근 20경기 중 커스텀만 자동 기록"으로 동작 요약.
- **라이엇 연동(`Riot/`)**: `RiotApiClient`(match-v5) + `RiotRateLimiter`(슬라이딩 윈도우, 기본 20req/1s·100req/2min).
  지역 라우팅은 `BotConfig.RiotRegion`(기본 asia=KR). 키(`RiotApiKey`) 미설정 시 `IsEnabled=false`로 이 명령만 비활성.
  - **`/내전기록하기 (대상)`**: 대상 PUUID로 최근 경기(기본 20·최대 20)를 조회 → `CUSTOM_GAME`만
    `MatchService.RecordDetailed`로 기록(참가 10명 팀/승패/라인). 라인은 `selectedRolePreferences`→
    `teamPosition`→`individualPosition`→`lane` 순으로 추정.
    - **실측 확인(2026-06, 커스텀 `KR_8266788793`)**: 라인 정하고 시작한 정상 5v5 내전은
      `teamPosition`이 10명 전원 정확히 채워짐(팀별 TOP/JUNGLE/MIDDLE/BOTTOM/UTILITY 하나씩).
      반면 같은 응답의 `lane`/`individualPosition`은 부정확(탑인데 `lane=JUNGLE` 등). 따라서
      **라인은 `teamPosition`을 신뢰**하면 되고, 특히 라인별 1v1 비교 같은 정밀 용도에는
      `teamPosition` 단독 사용이 안전(fallback이 `lane`까지 내려가면 오염됨). teamPosition이 비는 건
      리메이크·비정상 게임 정도로 드묾.
    **중복 방지**는 `Match.RiotMatchId`(이미 기록된 ID는 상세 호출도 생략). 미등록 참가자는 `PlayerId=0`+라이엇 닉네임 저장.
    내전러 승/패·승률은 기존처럼 경기 참여로부터 파생. **점수(팀 평균 Elo)·반창고 정산은 기록 시 자동 적용**되며 응답에 변동 요약 표시
    (**점수(Elo) 시스템**·**반창고 시스템** 섹션 참조). Elo는 순서 의존이라 후보를 `playedAt` 오름차순으로 기록.

## 점수(Elo) 시스템 (구현 완료)
경기 기록 시(`/내전기록하기` → `MatchService.RecordDetailed`/`ApplyScoring`) 내전러 점수를 **팀 평균 Elo**로 변동.
구현체는 `NewronaBot/EloScoringStrategy.cs`(공유 `IScoringStrategy`의 봇 전용 구현, `INewronaStore`에서 상수 로드).
WPF 경로는 그대로 `NoOpScoringStrategy`(SQLite, 점수 미반영).

- **공식**(팀 단위 — 한 팀의 모든 등록 내전러에게 **같은 delta** 적용):
  - `expected_T = 1 / (1 + 10^((상대팀평균 − 우리팀평균) / divisor))`  (제로섬: `expected1 + expected2 = 1`)
  - `delta_T = K × (팀승패(1/0) − expected_T)`  [ `+ B × (2×지표−1)` : 지표항 추후 구현 ]
  - 두 팀의 **평균 Elo**만 비교하므로 맞라인 1v1·`teamPosition` 라인 매칭이 **필요 없다**(라인 매칭 실패 경고도 사라짐).
- **상수**(Firestore `meta/eloConfig`에 저장 → 튜닝 가능, `NewronaDatabase.Elo`): `K=20`, `divisor=600`, `B=3`,
  `defaultUnregisteredScore=1300`. 문서가 없으면 기본값으로 시작하고 첫 flush 때 기록됨.
  - **divisor 마이그레이션**: 팀 평균 방식 전환에 맞춰 1v1 시절 기본값 `4000`→`600`. `FirestoreStore.InitializeAsync`가
    적재 시 저장값이 정확히 `4000`이면 자동으로 `600`으로 교체(다음 flush에 반영). 의도적으로 튜닝한 다른 값은 유지.
- **팀 평균에는 미등록 참가자(`PlayerId=0`)도 포함** — 그 사람 점수를 **기본 1300**으로 간주해 평균에 반영.
  단 delta는 **등록 내전러(`PlayerId>0`)에게만** 산출(미등록 본인은 점수 없음).
- **지표점수항(`B × (2×지표−1)`) 은 추후 구현** — 현재 `EloScoringStrategy`는 `delta_T = K×(승패−expected_T)`만 적용.
  구현 시 인플레이션 주의, 지표 기여는 `±B`(=±3)로 캡.
- **K항은 두 팀 사이에서 제로섬**(`expected1 + expected2 = 1`) → 인당 변동의 (팀1 합 = −팀2 합)는 등록 인원수가 같을 때 성립.
- **반올림: 내림(`Math.Floor`)** 으로 `int` delta 산출.
- **인터페이스**: `IScoringStrategy.CalculateDeltas(Match, IReadOnlyDictionary<int,int> currentScores)` → `ScoringResult`
  (`Deltas`=PlayerId별 증감, `Warnings`=경고). `MatchService.ApplyScoring`이 현재 점수 dict를 만들어 주입.
- **순서 의존성/멱등성 — (a) 경기별 delta 저장 방식**: 산출한 PlayerId별 delta를 `Match.ScoreDeltas`(저장)에 기록.
  **경기 삭제 시 그 delta를 역적용**(`DiscordMatchRepository.Delete`)해 점수 복구. 역적용은 저장된 delta 기준의 근사
  (Elo는 순서 의존이나 (a) 방식 채택으로 단순화). 적용 자체는 `playedAt` 오름차순.

## 반창고(:adhesive_bandage:) 시스템 (구현 완료)
부라인 배정의 '빚'을 누적해 라인 기반 팀 편성(`/팀짜주기라인`)에서 주라인 우선권을 주는 정수 카운터(`Player.Bandage`, 기본 0).
- **적립 시점 = 경기 기록 시**(`DiscordMatchRepository.Add`의 `AccrueBandages`, 봇 전용). 팀 편성 순간이 아니라
  실제 플레이 결과로 정산한다. 등록 내전러 + 주라인 데이터 있는 사람만 대상.
  - 실제 플레이 라인(`MatchPlayer.TeamPosition`→한글)이 **주라인이면 −1, 그 외(부/오프라인)면 +1**, **최소 0 클램프**.
  - 실제 적용분만 `Match.BandageDeltas`(저장)에 기록 → **경기 삭제 시 역적용**(최소 0 클램프, Elo와 같은 근사 복구).
  - 라인 정보 없는 수동 경기·`teamPosition` 누락 참가자는 정산 생략.
- **소비 시점 = 라인 기반 편성**(`/팀짜주기라인`): 부라인으로 밀려야 할 때 **반창고 많은 사람이 주라인**을 갖도록 점수화에 반영.
  편성은 반창고를 **읽기만** 하고 변경하지 않는다(변경은 경기 기록 때만).
- **표시**: `Player.BandageText`(`:adhesive_bandage:×N`). `/팀짜주기라인` 임베드, `/내전기록하기` 응답에 순변동 표시,
  `/내전러정보` 카드. (`/내전러목록`은 간소화되어 반창고 미표시 — 아래 명령어 표시 참조.)

## DB 스키마
- `Players(Id, Name, LolNickname, MainLane, SubLane, Puuid, Score)` — 라인은 콤마 join TEXT, 기본 Score 1000.
  (기존 DB는 `DatabaseInitializer`가 `ALTER TABLE ADD COLUMN`으로 MainLane/SubLane/Puuid 보강.)
- `Matches(Id, PlayedAt, Winner, Note)`
- `MatchPlayers(MatchId, PlayerId, Team)` — FK CASCADE, `PRAGMA foreign_keys=ON`
- 승/패는 `MatchPlayers.Team` vs `Matches.Winner` 비교로 집계(별도 저장 안 함).
- **반창고/Elo 관련 필드는 봇(Firestore) 전용** — 공유 모델에 `Player.Bandage`, `Match.BandageDeltas/ScoreDeltas` 추가(additive)지만
  WPF SQLite 경로는 미사용(기본 0/빈 dict, 스키마·`DatabaseInitializer` 변경 없음).

## 변경 이력
- 기존 .NET Framework 4.7.2 `WpfApp1` 템플릿 → **SDK 스타일 .NET 9 WPF로 전환**.
  - 삭제: `Properties/`, `App.config`, 구형 csproj 참조.
  - 루트 네임스페이스 `NewronaData`, 어셈블리명 `WpfApp1` 유지.
- 전체 기능(플레이어/경기/등급) 및 SQLite 영속화 신규 구현.
- **디스코드 봇 `NewronaBot` 추가** — 서비스/도메인 계층 재사용, WPF 앱은 그대로 유지(additive).
  저장소를 **디스코드 채널 JSON → Firestore로 전환**(`INewronaStore` 추상화로 교체).
- **라이엇 match-v5 연동 추가** — `/내전기록하기`로 커스텀 게임 자동 기록(빈도 제한 준수). `/등급`에 점수 기준표 선표시.
  공유 모델 확장(additive): `Match.RiotMatchId`, `MatchPlayer.Lane/Puuid`. WPF SQLite 경로는 이 필드 미사용(기본값).
- **점수(Elo) 시스템 구현** — 최초 맞라인 1v1 Elo로 구현 후 **팀 평균 Elo로 변경**(`K=20`/`divisor=600`/`B=3`, Firestore `meta/eloConfig`).
  `EloScoringStrategy`(봇 전용 `IScoringStrategy` 구현), `IScoringStrategy`는 현재 점수 주입 + `ScoringResult`(Deltas/Warnings) 반환.
  공유 모델 확장(additive): `Match.ScoreDeltas`(저장)/`Match.ScoringWarnings`(일시), `MatchPlayer.TeamPosition`.
  경기별 delta 저장 후 삭제 시 역적용(`DiscordMatchRepository.Delete`). 지표점수항·캡은 추후 구현. WPF는 NoOp 유지.
- **점수 산정 방식 변경** — 맞라인 1v1 → **팀 평균 Elo**(팀원 전원 동일 delta), `divisor` `4000`→`600`(`InitializeAsync` 자동 마이그레이션).
  라인 매칭 불필요해짐(매칭 실패 경고 제거).
- **팀 자동 편성 명령 추가** — `/팀짜주기뱀`(점수순 스네이크), `/팀짜주기랜덤`(평균 Elo차 ≤40 랜덤, 불가 시 최근접+경고).
  `Commands/TeamCommands.cs`(`[RequireRole("내전관리자")]`), `Player.Score`만 사용·라인 무시. 봇 전용(공유 모델 변경 없음).
- **반창고 시스템 + `/팀짜주기라인` 추가** — 부라인 빚(`Player.Bandage`) 누적으로 라인 기반 편성에서 주라인 우선권.
  경기 기록 시 정산(주라인 −1/그 외 +1, 최소 0), `Match.BandageDeltas`(저장) 삭제 시 역적용. `/팀짜주기라인`은 사전식
  우선순위(주라인 커버 → 강제 최소 → 반창고 → 가상 Elo 균형: 부 −50·강제 −150)로 전수 탐색. 공유 모델 확장(additive):
  `Player.Bandage`, `Match.BandageDeltas`. WPF SQLite 경로 미사용.
