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

    [SlashCommand("등급기준표", "점수→등급 기준표를 봅니다.")]
    public async Task Criteria()
    {
        await RespondAsync(embed: CriteriaEmbed(), ephemeral: true);
    }

    [SlashCommand("서버내등급", "서버 내 내전러 등급을 봅니다.")]
    public async Task ServerRanks()
    {
        var groups = _rank.Group(_players.GetPlayers());
        if (groups.Count == 0)
        {
            await RespondAsync("표시할 내전러가 없습니다. `/내전러추가` 로 등록하세요.", ephemeral: true);
            return;
        }

        var lines = groups.Select(g => $"**{g.Rank.Name}** = {g.PlayersText}");
        var embed = new EmbedBuilder()
            .WithTitle("🏆 서버 내 등급")
            .WithDescription(string.Join("\n", lines))
            .WithColor(Color.Purple)
            .Build();
        await RespondAsync(embed: embed, ephemeral: true);
    }

    /// <summary>점수→등급 기준표 임베드(높은 등급부터, 각 등급의 최소 점수).</summary>
    private Embed CriteriaEmbed()
    {
        // `### ` 헤더 마크다운 → 일반 텍스트의 약 1.5배 크기로 렌더링.
        // 줄 사이는 `\n\n`(빈 줄 1칸)으로 띄움.
        var lines = _rank.Ranks
            .OrderByDescending(r => r.MinScore)
            .Select(r => $"### {r.Name} — {r.MinScore}점 이상");

        return new EmbedBuilder()
            .WithTitle("📋 등급 기준표 (점수 기준)")
            .WithDescription(string.Join("\n\n", lines))
            .WithColor(Color.LightGrey)
            .WithFooter("신규 내전러 기본 점수 1000(:cat:)")
            .Build();
    }
}
