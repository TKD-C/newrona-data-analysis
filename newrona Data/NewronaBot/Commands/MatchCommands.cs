using Discord;
using Discord.Interactions;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaBot.Commands;

/// <summary>경기 기록 슬래시 명령어.</summary>
public sealed class MatchCommands : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly char[] Separators = { ',', ' ', '\n', '\t', '/' };

    private readonly IMatchService _matches;
    private readonly IPlayerService _players;

    public MatchCommands(IMatchService matches, IPlayerService players)
    {
        _matches = matches;
        _players = players;
    }

    [SlashCommand("경기기록", "5대5 경기 결과를 기록합니다(각 팀 5명, 이름을 공백/쉼표로 구분).")]
    public async Task Record(
        [Summary("팀1", "1팀 5명의 이름 (예: 철수 영희 민수 지훈 수빈)")] string 팀1,
        [Summary("팀2", "2팀 5명의 이름")] string 팀2,
        [Summary("승리팀", "이긴 팀")]
        [Choice("1팀", 1)] [Choice("2팀", 2)] int 승리팀,
        [Summary("비고", "메모(선택)")] string 비고 = "")
    {
        await DeferAsync();
        try
        {
            var roster = _players.GetPlayers();
            var t1 = Resolve(팀1, roster);
            var t2 = Resolve(팀2, roster);

            var match = _matches.Record(t1, t2, (Team)승리팀, DateTime.Now, 비고);

            var embed = new EmbedBuilder()
                .WithTitle("✅ 경기 기록 완료")
                .WithDescription(match.Summary)
                .WithColor(Color.Green)
                .Build();
            await FollowupAsync(embed: embed);
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}");
        }
    }

    [SlashCommand("경기목록", "최근 경기 기록을 봅니다.")]
    public async Task List([Summary("개수", "표시할 개수(기본 10)")] int 개수 = 10)
    {
        var matches = _matches.GetMatches().Take(Math.Clamp(개수, 1, 25)).ToList();
        if (matches.Count == 0)
        {
            await RespondAsync("기록된 경기가 없습니다.");
            return;
        }

        var lines = matches.Select(m => $"`#{m.Id}` {m.Summary}");
        var embed = new EmbedBuilder()
            .WithTitle("📜 경기 기록")
            .WithDescription(string.Join("\n", lines))
            .WithColor(Color.Gold)
            .WithFooter("삭제: /경기삭제 [번호]")
            .Build();
        await RespondAsync(embed: embed);
    }

    [SlashCommand("경기삭제", "경기 기록을 삭제합니다(번호로).")]
    public async Task Delete([Summary("번호", "경기목록의 # 번호")] int 번호)
    {
        var exists = _matches.GetMatches().Any(m => m.Id == 번호);
        if (!exists)
        {
            await RespondAsync($"⚠️ #{번호} 경기를 찾을 수 없습니다.", ephemeral: true);
            return;
        }

        _matches.Delete(번호);
        await RespondAsync($"🗑️ #{번호} 경기를 삭제했습니다.");
    }

    /// <summary>입력 문자열을 플레이어 이름으로 분리해 ID로 변환. 미등록 이름이 있으면 예외.</summary>
    private static List<int> Resolve(string raw, IReadOnlyList<Player> roster)
    {
        var names = raw.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ids = new List<int>();
        var missing = new List<string>();

        foreach (var name in names)
        {
            var hit = roster.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (hit is null) missing.Add(name);
            else ids.Add(hit.Id);
        }

        if (missing.Count > 0)
            throw new InvalidOperationException($"등록되지 않은 플레이어: {string.Join(", ", missing)} — 먼저 /플레이어추가 로 등록하세요.");

        return ids;
    }
}
