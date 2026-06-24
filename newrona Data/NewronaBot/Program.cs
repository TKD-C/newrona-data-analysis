using System.Reflection;
using System.Text.Json;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NewronaBot;
using NewronaBot.Data;
using NewronaBot.Persistence;
using NewronaBot.Riot;
using NewronaData.Data;
using NewronaData.Services;

// ── 점검 모드: 디스코드 없이 Firestore만 다룬다(설정과 독립) ────
//   dotnet run -- seed [이름]        : 플레이어 1명 추가
//   dotnet run -- delete <이름...>   : 이름으로 플레이어 삭제
//   dotnet run -- list               : 현재 플레이어 목록
if (args.Length > 0 && args[0] is "seed" or "delete" or "list")
{
    var keyPath = Environment.GetEnvironmentVariable("NEWRONA_FIREBASE_KEY") ?? "firebase-key.json";
    if (!Path.IsPathRooted(keyPath)) keyPath = Path.Combine(AppContext.BaseDirectory, keyPath);
    if (!File.Exists(keyPath)) { Console.WriteLine($"❌ 키 파일 없음: {keyPath}"); return; }

    var projectId = Environment.GetEnvironmentVariable("NEWRONA_FIREBASE_PROJECT");
    if (string.IsNullOrWhiteSpace(projectId))
        projectId = JsonDocument.Parse(File.ReadAllText(keyPath)).RootElement.GetProperty("project_id").GetString();
    Console.WriteLine($"🔌 Firestore 연결: project={projectId}");

    INewronaStore cliStore = FirestoreStore.Create(projectId!, keyPath);
    await cliStore.InitializeAsync();
    var cliPlayers = new PlayerService(new DiscordPlayerRepository(cliStore));

    switch (args[0])
    {
        case "seed":
            var created = cliPlayers.Create(args.Length > 1 ? args[1] : "테스트플레이어", "Hide on bush", 2000);
            Console.WriteLine($"➕ 추가: {created.Name} (Id={created.Id}, 점수 {created.Score})");
            break;

        case "delete":
            foreach (var target in args.Skip(1))
            {
                var found = cliPlayers.GetPlayers().FirstOrDefault(p => string.Equals(p.Name, target, StringComparison.OrdinalIgnoreCase));
                if (found is null) { Console.WriteLine($"⚠️ '{target}' 없음(건너뜀)"); continue; }
                cliPlayers.Delete(found.Id);
                Console.WriteLine($"🗑️ 삭제: {found.Name} (Id={found.Id})");
            }
            break;
    }

    await cliStore.FlushAsync(); // 디바운스 기다리지 않고 즉시 저장
    Console.WriteLine("📋 현재 플레이어 목록:");
    var remaining = cliPlayers.GetPlayers();
    if (remaining.Count == 0) Console.WriteLine("  (없음)");
    foreach (var p in remaining)
        Console.WriteLine($"  - {p.Name} · 점수 {p.Score} · {p.Wins}승 {p.Losses}패");
    Console.WriteLine("✅ 완료. Firebase 콘솔의 Firestore에서 players 컬렉션을 확인하세요.");
    return;
}

// ── 설정 로드 ────────────────────────────────────────────────
var config = BotConfig.Load();
if (config is null)
{
    Console.WriteLine("""
        ❌ 설정이 없습니다. 아래 중 하나로 토큰/ID를 제공하세요.

        [환경변수]
          NEWRONA_BOT_TOKEN        = 디스코드 봇 토큰
          NEWRONA_GUILD_ID         = 서버(길드) ID
          NEWRONA_FIREBASE_PROJECT = Firebase 프로젝트 ID
          NEWRONA_FIREBASE_KEY     = 서비스 계정 키 JSON 경로(상대경로면 실행 폴더 기준)

        [또는 실행 폴더의 botconfig.json]  (botconfig.example.json 참고)
        """);
    return;
}

// ── DI 조립(합성 루트) ───────────────────────────────────────
var socketConfig = new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds, // 슬래시 명령어만 사용 → 최소 권한
    LogLevel = LogSeverity.Info,
};

var client = new DiscordSocketClient(socketConfig);
var interactions = new InteractionService(client, new InteractionServiceConfig { DefaultRunMode = RunMode.Async });
INewronaStore store = FirestoreStore.Create(config.FirebaseProjectId, config.FirebaseKeyPath);

var services = new ServiceCollection()
    .AddSingleton(client)
    .AddSingleton(interactions)
    .AddSingleton(store)
    .AddSingleton<IPlayerRepository>(_ => new DiscordPlayerRepository(store))
    .AddSingleton<IMatchRepository>(_ => new DiscordMatchRepository(store))
    .AddSingleton<IScoringStrategy>(_ => new EloScoringStrategy(store)) // 맞라인 1v1 Elo(상수: Firestore meta/eloConfig)
    .AddSingleton<IRankService>(_ => new RankService())
    .AddSingleton<IPlayerService, PlayerService>()
    .AddSingleton<IMatchService, MatchService>()
    // 라이엇 연동: 빈도 제한기(20/1s·100/2min) + match-v5 클라이언트. 키 없으면 비활성.
    .AddSingleton(new RiotRateLimiter())
    .AddSingleton(sp => new RiotApiClient(config.RiotApiKey, config.RiotRegion, sp.GetRequiredService<RiotRateLimiter>()))
    .BuildServiceProvider();

if (config.RiotApiKey is null)
    Console.WriteLine("ℹ️ RIOT_API_KEY 미설정 → /내전기록하기 비활성(나머지 기능은 정상).");

// ── 로깅 ─────────────────────────────────────────────────────
client.Log += msg => { Console.WriteLine($"[{msg.Severity}] {msg.Source}: {msg.Message} {msg.Exception}"); return Task.CompletedTask; };
interactions.Log += msg => { Console.WriteLine($"[{msg.Severity}] {msg.Source}: {msg.Message} {msg.Exception}"); return Task.CompletedTask; };

// ── 준비 완료 시: 데이터 적재 + 명령어 등록 ──────────────────
client.Ready += async () =>
{
    try
    {
        await store.InitializeAsync();
        await interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), services);
        await interactions.RegisterCommandsToGuildAsync(config.GuildId, deleteMissing: true);
        Console.WriteLine($"🤖 봇 준비 완료: {client.CurrentUser} — 명령어를 서버에 등록했습니다.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ 초기화 실패: {ex.Message}\n{ex}");
    }
};

// ── 슬래시 명령 실행 라우팅 ──────────────────────────────────
client.InteractionCreated += async interaction =>
{
    try
    {
        var ctx = new SocketInteractionContext(client, interaction);
        await interactions.ExecuteCommandAsync(ctx, services);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"명령 실행 오류: {ex.Message}");
    }
};

// ── 로그인 및 시작 ───────────────────────────────────────────
await client.LoginAsync(TokenType.Bot, config.Token);
await client.StartAsync();

// ── 안전 종료: Ctrl+C 시 마지막 상태를 채널에 저장 ───────────
var stopSignal = new TaskCompletionSource();
Console.CancelKeyPress += async (_, e) =>
{
    e.Cancel = true;
    Console.WriteLine("종료 중… 데이터 저장");
    await store.FlushAsync();
    await client.StopAsync();
    stopSignal.TrySetResult();
};

Console.WriteLine("실행 중입니다. 종료하려면 Ctrl+C 를 누르세요.");
await stopSignal.Task;
