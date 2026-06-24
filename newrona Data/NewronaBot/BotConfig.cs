using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewronaBot;

/// <summary>
/// 봇 실행에 필요한 설정. 환경변수 우선, 없으면 실행 폴더의 botconfig.json에서 읽음.
/// - NEWRONA_BOT_TOKEN        : 디스코드 봇 토큰
/// - NEWRONA_GUILD_ID         : 명령어를 등록할 서버(길드) ID
/// - NEWRONA_FIREBASE_PROJECT : Firebase 프로젝트 ID(데이터 저장)
/// - NEWRONA_FIREBASE_KEY     : 서비스 계정 키 JSON 경로(상대경로면 실행 폴더 기준)
/// - RIOT_API_KEY             : 라이엇 API 키 (선택)
/// </summary>
public sealed class BotConfig
{
    public required string Token { get; init; }
    public required ulong GuildId { get; init; }

    /// <summary>Firebase 프로젝트 ID(데이터를 저장할 Firestore 프로젝트).</summary>
    public required string FirebaseProjectId { get; init; }

    /// <summary>서비스 계정 키 JSON의 절대경로.</summary>
    public required string FirebaseKeyPath { get; init; }

    /// <summary>라이엇 API 키. 미설정 시 null(라이엇 연동 기능 비활성).</summary>
    public string? RiotApiKey { get; init; }

    /// <summary>match-v5 지역 라우팅(americas/asia/europe). 한국(KR)은 asia. 기본 asia.</summary>
    public string RiotRegion { get; init; } = "asia";

    public static BotConfig? Load()
    {
        var token = Environment.GetEnvironmentVariable("NEWRONA_BOT_TOKEN");
        var guild = Environment.GetEnvironmentVariable("NEWRONA_GUILD_ID");
        var project = Environment.GetEnvironmentVariable("NEWRONA_FIREBASE_PROJECT");
        var keyPath = Environment.GetEnvironmentVariable("NEWRONA_FIREBASE_KEY");
        var riotKey = Environment.GetEnvironmentVariable("RIOT_API_KEY");
        var riotRegion = Environment.GetEnvironmentVariable("RIOT_REGION");

        var path = Path.Combine(AppContext.BaseDirectory, "botconfig.json");
        if ((token is null || guild is null || project is null || keyPath is null || riotKey is null || riotRegion is null) && File.Exists(path))
        {
            try
            {
                var file = JsonSerializer.Deserialize<FileConfig>(File.ReadAllText(path));
                token ??= file?.Token;
                guild ??= file?.GuildId;
                project ??= file?.FirebaseProjectId;
                keyPath ??= file?.FirebaseKeyPath;
                riotKey ??= file?.RiotApiKey;
                riotRegion ??= file?.RiotRegion;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ botconfig.json 읽기 실패: {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(token)
            || !ulong.TryParse(guild, out var guildId)
            || string.IsNullOrWhiteSpace(project)
            || string.IsNullOrWhiteSpace(keyPath))
        {
            return null;
        }

        // 상대경로면 실행 폴더 기준으로 절대경로화.
        var resolvedKeyPath = Path.IsPathRooted(keyPath)
            ? keyPath
            : Path.Combine(AppContext.BaseDirectory, keyPath);

        if (!File.Exists(resolvedKeyPath))
        {
            Console.WriteLine($"❌ Firebase 서비스 계정 키를 찾을 수 없습니다: {resolvedKeyPath}");
            return null;
        }

        return new BotConfig
        {
            Token = token,
            GuildId = guildId,
            FirebaseProjectId = project,
            FirebaseKeyPath = resolvedKeyPath,
            RiotApiKey = string.IsNullOrWhiteSpace(riotKey) ? null : riotKey,
            RiotRegion = string.IsNullOrWhiteSpace(riotRegion) ? "asia" : riotRegion.Trim().ToLowerInvariant(),
        };
    }

    private sealed class FileConfig
    {
        [JsonPropertyName("token")] public string? Token { get; set; }
        [JsonPropertyName("guildId")] public string? GuildId { get; set; }
        [JsonPropertyName("firebaseProjectId")] public string? FirebaseProjectId { get; set; }
        [JsonPropertyName("firebaseKeyPath")] public string? FirebaseKeyPath { get; set; }
        [JsonPropertyName("riotApiKey")] public string? RiotApiKey { get; set; }
        [JsonPropertyName("riotRegion")] public string? RiotRegion { get; set; }
    }
}
