using Discord;
using Discord.Interactions;
using NewronaData.Services;

namespace NewronaBot.Commands;

/// <summary>서버 내 등급표 슬래시 명령어.</summary>
public sealed class RankCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IPlayerService _players;
    private readonly IRankService _rank;

    public RankCommands(IPlayerService players, IRankService rank)
    {
        _players = players;
        _rank = rank;
    }

    [SlashCommand("등급", "서버 내 등급표(점수 기준)를 봅니다.")]
    public async Task Show()
    {
        var groups = _rank.Group(_players.GetPlayers());
        if (groups.Count == 0)
        {
            await RespondAsync("표시할 플레이어가 없습니다. `/플레이어추가` 로 등록하세요.");
            return;
        }

        var lines = groups.Select(g => $"**{g.Rank.Name}** = {g.PlayersText}");
        var embed = new EmbedBuilder()
            .WithTitle("🏆 서버 내 등급")
            .WithDescription(string.Join("\n", lines))
            .WithColor(Color.Purple)
            .Build();
        await RespondAsync(embed: embed);
    }
}
