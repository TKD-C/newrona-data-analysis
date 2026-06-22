# 뉴로나 내전 봇 (NewronaBot)

기존 WPF 데스크톱 앱의 **도메인/서비스 로직을 그대로 재사용**하는 디스코드 봇.
데이터는 **전용 디스코드 채널의 고정 메시지에 `newrona-data.json` 첨부파일**로 저장됩니다(별도 DB/서버 불필요).

## 1. 디스코드 봇 만들기 (최초 1회)

1. https://discord.com/developers/applications → **New Application** 생성
2. 좌측 **Bot** 탭 → **Reset Token** 으로 토큰 확인 (이 값이 `token`)
   - 이 봇은 슬래시 명령어만 쓰므로 *Privileged Intents* 는 켤 필요 없음
3. 좌측 **OAuth2 → URL Generator**
   - **Scopes**: `bot`, `applications.commands`
   - **Bot Permissions**: `Send Messages`, `Embed Links`, `Attach Files`, `Manage Messages`(메시지 고정용), `Read Message History`
   - 생성된 URL로 봇을 내 서버에 초대

## 2. ID 얻기

디스코드 설정 → 고급 → **개발자 모드 ON** 후:
- 서버 아이콘 우클릭 → **서버 ID 복사** → `guildId`
- 데이터 저장용 채널(예: `#봇-데이터`, 일반 멤버는 안 보이게) 우클릭 → **채널 ID 복사** → `channelId`

## 3. 설정

`botconfig.example.json` 을 복사해 `botconfig.json` 으로 만들고 값을 채우세요.
(또는 환경변수 `NEWRONA_BOT_TOKEN` / `NEWRONA_GUILD_ID` / `NEWRONA_CHANNEL_ID` 사용)

> ⚠️ `botconfig.json`(토큰 포함)은 절대 공개 저장소에 올리지 마세요.

## 4. 실행

```
dotnet run --project "NewronaBot/NewronaBot.csproj"
```

`🤖 봇 준비 완료` 가 뜨면 디스코드에서 `/` 입력 시 명령어가 보입니다.

## 명령어

| 명령어 | 설명 |
|--------|------|
| `/플레이어추가 이름 [롤닉네임] [티어] [점수]` | 플레이어 등록 |
| `/플레이어목록` | 플레이어 + 승/패/승률 |
| `/플레이어수정 대상 [새이름] [롤닉네임] [티어] [점수]` | 정보 수정 |
| `/플레이어삭제 대상` | 삭제 |
| `/경기기록 팀1 팀2 승리팀 [비고]` | 5대5 결과 기록 (이름 공백/쉼표 구분) |
| `/경기목록 [개수]` | 최근 경기 |
| `/경기삭제 번호` | 경기 삭제 |
| `/등급` | 서버 내 등급표 |

예: `/경기기록 팀1:"철수 영희 민수 지훈 수빈" 팀2:"가 나 다 라 마" 승리팀:1팀`

## 저장 방식 / 주의

- 실행 중에는 **메모리가 원본**이며, 변경 시 약 1.2초 뒤 채널 메시지에 자동 저장(연속 변경은 합쳐 저장).
- 백업: 채널의 고정 메시지에서 `newrona-data.json` 을 내려받으면 됩니다.
- 그 고정 메시지/첨부를 멤버가 지우면 안 됩니다(다음 저장 때 새로 생성되지만 그 전 내용은 유실).
- 기존 WPF 앱(SQLite)과 봇은 **데이터가 분리**되어 있습니다. 기존 데이터 이전이 필요하면 알려주세요.
