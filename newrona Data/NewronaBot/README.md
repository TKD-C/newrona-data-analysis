# 뉴로나 내전 봇 (NewronaBot)

기존 WPF 데스크톱 앱의 **도메인/서비스 로직을 그대로 재사용**하는 디스코드 봇.
데이터는 **Google Cloud Firestore(무료 Spark 플랜)** 에 저장됩니다.

## 1. 디스코드 봇 만들기 (최초 1회)

1. https://discord.com/developers/applications → **New Application** 생성
2. 좌측 **Bot** 탭 → **Reset Token** 으로 토큰 확인 (이 값이 `token`)
   - 이 봇은 슬래시 명령어만 쓰므로 *Privileged Intents* 는 켤 필요 없음
3. 좌측 **OAuth2 → URL Generator**
   - **Scopes**: `bot`, `applications.commands`
   - **Bot Permissions**: `Send Messages`, `Embed Links`, `Attach Files`, `Manage Messages`(메시지 고정용), `Read Message History`
   - 생성된 URL로 봇을 내 서버에 초대

## 2. 서버(길드) ID 얻기

디스코드 설정 → 고급 → **개발자 모드 ON** 후:
- 서버 아이콘 우클릭 → **서버 ID 복사** → `guildId`

## 3. Firebase 준비 (최초 1회)

1. https://console.firebase.google.com → **프로젝트 추가** (무료 Spark 플랜으로 충분)
2. 좌측 **빌드 → Firestore Database → 데이터베이스 만들기**
   - 위치는 가까운 리전(예: `asia-northeast3` 서울), 모드는 **프로덕션 모드**로 시작
3. **프로젝트 설정(⚙️) → 서비스 계정 → 새 비공개 키 생성** → 내려받은 JSON을
   `NewronaBot/firebase-key.json` 으로 저장 (`.gitignore`에 이미 등록되어 커밋 안 됨)
4. **프로젝트 설정 → 일반** 의 **프로젝트 ID** 를 `firebaseProjectId` 로 사용

> 서버 앱이라 사용자 로그인이 아니라 **서비스 계정 키**로 접속합니다.
> 이 키는 관리자 권한이므로 절대 공개 저장소에 올리지 마세요.

## 4. 설정

`botconfig.example.json` 을 복사해 `botconfig.json` 으로 만들고 값을 채우세요.
(또는 환경변수 `NEWRONA_BOT_TOKEN` / `NEWRONA_GUILD_ID` / `NEWRONA_FIREBASE_PROJECT` / `NEWRONA_FIREBASE_KEY` 사용)

> ⚠️ `botconfig.json`(토큰 포함)과 `firebase-key.json` 은 절대 공개 저장소에 올리지 마세요.

## 5. 실행

```
dotnet run --project "NewronaBot/NewronaBot.csproj"
```

`🤖 봇 준비 완료` 가 뜨면 디스코드에서 `/` 입력 시 명령어가 보입니다.

## 명령어

| 명령어 | 설명 |
|--------|------|
| `/내전러추가 이름 [롤닉네임] [주라인1·2] [부라인1·2] [점수]` | 내전러 등록 (라인 최대 2개씩) |
| `/내전러목록` | 내전러 + 라인 + 승/패/승률 |
| `/내전러수정 대상 [새이름] [롤닉네임] [주라인1·2] [부라인1·2] [점수]` | 정보 수정 |
| `/내전러삭제 대상` | 삭제 |
| `/내전러puuid설정 대상 puuid` | **[관리자]** PUUID(비밀) 설정 — 본인에게만 표시 |
| `/내전러puuid조회 대상` | **[관리자]** PUUID(비밀) 조회 — 본인에게만 표시 |
| `/경기기록 팀1 팀2 승리팀 [비고]` | 5대5 결과 수동 기록 (이름 공백/쉼표 구분) |
| `/내전기록하기 대상 [개수]` | 라이엇에서 최근 경기를 가져와 **내전(커스텀)만 자동 기록** (PUUID 필요) |
| `/경기목록 [개수]` | 최근 경기 |
| `/경기삭제 번호` | 경기 삭제 |
| `/등급` | 등급 기준표 + 서버 내 내전러 등급 |

> PUUID는 **비밀 정보**라 일반 명령/목록에는 노출되지 않으며, 서버 관리자만 설정·조회할 수 있습니다(응답은 호출자에게만 보임).

### `/내전기록하기` (라이엇 연동)

- 대상 내전러의 **PUUID**로 최근 경기(기본 20개, 최대 20)를 조회 → `gameType=CUSTOM_GAME` 만 골라 Firestore에 기록합니다.
- 기록 내용: 참가 10명의 **팀 / 승패 / 라인**(`selectedRolePreferences`→`teamPosition`→`individualPosition` 순으로 추정). 참가자 중 **PUUID가 등록된 내전러**는 그 전적(승/패·승률)에 자동 반영되어 `/내전러목록` 에서 조회됩니다. (점수 변동은 추후 구현)
- **중복 방지**: 이미 기록된 라이엇 match ID는 건너뜁니다(상세 호출도 생략해 API 호출량 절약).
- **API 호출량**: 개발용 키 한도(20req/1s, 100req/2min, 라우팅별)를 지키도록 내부 빈도 제한기를 둡니다. 1회 호출 시 최대 `1 + 개수`(≤21) 요청.
- 활성화하려면 `riotApiKey`(또는 `RIOT_API_KEY`)가 필요하며, 미설정 시 이 명령만 비활성화됩니다.
- 지역 라우팅은 `riotRegion`(기본 `asia`, 한국 KR=asia). 환경변수 `RIOT_REGION` 로도 지정 가능.

예: `/경기기록 팀1:"철수 영희 민수 지훈 수빈" 팀2:"가 나 다 라 마" 승리팀:1팀`

## 저장 방식 / 주의

- 실행 중에는 **메모리가 원본**이며, 변경 시 약 1.2초 뒤 Firestore에 자동 저장(연속 변경은 합쳐 저장).
- 컬렉션 구조: `players/{id}`, `matches/{id}`, `meta/counters`.
- 무료 Spark 플랜 한도(하루 읽기 5만 / 쓰기·삭제 각 2만)는 소규모 내전 봇에 충분합니다.
- 백업: Firebase 콘솔의 Firestore 데이터 뷰에서 직접 확인하거나 내보낼 수 있습니다.
- 기존 WPF 앱(SQLite)과 봇은 **데이터가 분리**되어 있습니다. 기존 데이터 이전이 필요하면 알려주세요.
