using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NewronaBot;
using NewronaBot.Data;
using NewronaBot.Persistence;
using NewronaData.Data;
using NewronaData.Services;

// ── 설정 로드 ────────────────────────────────────────────────
var config = BotConfig.Load();
if (config is null)
{
    Console.WriteLine("""
        ❌ 설정이 없습니다. 아래 중 하나로 토큰/ID를 제공하세요.

        [환경변수]
          NEWRONA_BOT_TOKEN   = 디스코드 봇 토큰
          NEWRONA_GUILD_ID    = 서버(길드) ID
          NEWRONA_CHANNEL_ID  = 데이터 저장 전용 채널 ID

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
var store = new DiscordJsonStore(client, config.ChannelId);

var services = new ServiceCollection()
    .AddSingleton(client)
    .AddSingleton(interactions)
    .AddSingleton(store)
    .AddSingleton<IPlayerRepository>(_ => new DiscordPlayerRepository(store))
    .AddSingleton<IMatchRepository>(_ => new DiscordMatchRepository(store))
    .AddSingleton<IScoringStrategy, NoOpScoringStrategy>()      // 점수 시스템 추후 교체 지점
    .AddSingleton<IRankService>(_ => new RankService())
    .AddSingleton<IPlayerService, PlayerService>()
    .AddSingleton<IMatchService, MatchService>()
    .BuildServiceProvider();

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
