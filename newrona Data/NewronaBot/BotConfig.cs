using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewronaBot;

/// <summary>
/// 봇 실행에 필요한 설정. 환경변수 우선, 없으면 실행 폴더의 botconfig.json에서 읽음.
/// - NEWRONA_BOT_TOKEN  : 디스코드 봇 토큰
/// - NEWRONA_GUILD_ID   : 명령어를 등록할 서버(길드) ID
/// - NEWRONA_CHANNEL_ID : 데이터(JSON)를 저장할 전용 채널 ID
/// </summary>
public sealed class BotConfig
{
    public required string Token { get; init; }
    public required ulong GuildId { get; init; }
    public required ulong ChannelId { get; init; }

    public static BotConfig? Load()
    {
        var token = Environment.GetEnvironmentVariable("NEWRONA_BOT_TOKEN");
        var guild = Environment.GetEnvironmentVariable("NEWRONA_GUILD_ID");
        var channel = Environment.GetEnvironmentVariable("NEWRONA_CHANNEL_ID");

        var path = Path.Combine(AppContext.BaseDirectory, "botconfig.json");
        if ((token is null || guild is null || channel is null) && File.Exists(path))
        {
            try
            {
                var file = JsonSerializer.Deserialize<FileConfig>(File.ReadAllText(path));
                token ??= file?.Token;
                guild ??= file?.GuildId;
                channel ??= file?.ChannelId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ botconfig.json 읽기 실패: {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(token)
            || !ulong.TryParse(guild, out var guildId)
            || !ulong.TryParse(channel, out var channelId))
        {
            return null;
        }

        return new BotConfig { Token = token, GuildId = guildId, ChannelId = channelId };
    }

    private sealed class FileConfig
    {
        [JsonPropertyName("token")] public string? Token { get; set; }
        [JsonPropertyName("guildId")] public string? GuildId { get; set; }
        [JsonPropertyName("channelId")] public string? ChannelId { get; set; }
    }
}
