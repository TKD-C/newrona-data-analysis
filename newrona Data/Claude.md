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
1. **플레이어** 추가/수정/삭제 + 목록(승·패·승률 집계 표시) — `플레이어` 탭
2. **5대5 경기** 수동 입력(팀별 5명 선택, 승리 팀, 일시, 비고) 및 기록 목록/삭제 — `경기 기록` 탭
3. **서버 내 등급** 점수 기준 정렬 표시 (예: `OP = 서버원1, 서버원2`) — `서버 등급` 탭

## 서버 내 롤 등급표 (점수 기준)
OP≥200, ★★★≥190, ★★≥180, ★≥170, 칼칼칼≥160, 칼칼≥150, 칼≥140,
번번번≥130, 번번≥120, 번≥110, 고양이≥100
- `Services/RankService.cs`의 `Default` 배열로 관리(주입 가능 → OCP).

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
- **점수 시스템은 미구현** — `IScoringStrategy`만 정의, 기본 `NoOpScoringStrategy` 주입.
  추후 Elo 등 구현체로 교체하면 경기 저장 시 자동 점수 반영(`MatchService.ApplyScoring`).
- 등급은 점수에서 파생(`RankService.Resolve`). DB에는 점수만 저장.
- 저장소 인터페이스(DIP)로 SQLite 외 다른 저장소로 교체 가능.
  → 디스코드 봇(`NewronaBot`)이 바로 이 점을 활용: `IPlayerRepository`/`IMatchRepository`만
     디스코드 채널 JSON 구현으로 교체하고 `Services` 계층 전체를 복제 없이 재사용.
- 등급표 외부 주입 가능(OCP).

## 디스코드 봇 (NewronaBot/)
- 콘솔 앱(`net9.0`) + Discord.Net. WPF 앱과 **데이터/실행 분리**(SQLite 미사용).
- 도메인/서비스 코드는 csproj `<Compile Include>` 링크로 **WpfApp1에서 공유**(복제 없음).
- 저장: 전용 채널의 고정 메시지에 `newrona-data.json` 첨부(`Persistence/DiscordJsonStore`).
  메모리가 실시간 원본 → 변경 시 디바운스 후 채널에 자동 저장, 시작 시 채널에서 적재.
- 명령어: `Commands/`의 슬래시 모듈(플레이어/경기/등급). 설정·실행법은 `NewronaBot/README.md`.

## DB 스키마
- `Players(Id, Name, LolNickname, LolTier, Score)`
- `Matches(Id, PlayedAt, Winner, Note)`
- `MatchPlayers(MatchId, PlayerId, Team)` — FK CASCADE, `PRAGMA foreign_keys=ON`
- 승/패는 `MatchPlayers.Team` vs `Matches.Winner` 비교로 집계(별도 저장 안 함).

## 변경 이력
- 기존 .NET Framework 4.7.2 `WpfApp1` 템플릿 → **SDK 스타일 .NET 9 WPF로 전환**.
  - 삭제: `Properties/`, `App.config`, 구형 csproj 참조.
  - 루트 네임스페이스 `NewronaData`, 어셈블리명 `WpfApp1` 유지.
- 전체 기능(플레이어/경기/등급) 및 SQLite 영속화 신규 구현.
- **디스코드 봇 `NewronaBot` 추가** — 데이터를 전용 채널 JSON으로 저장.
  서비스/도메인 계층 재사용, WPF 앱은 그대로 유지(additive).
