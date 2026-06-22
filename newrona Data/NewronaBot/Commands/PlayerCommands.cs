using Discord;
using Discord.Interactions;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaBot.Commands;

/// <summary>플레이어 관리 슬래시 명령어.</summary>
public sealed class PlayerCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IPlayerService _players;
    private readonly IRankService _rank;

    public PlayerCommands(IPlayerService players, IRankService rank)
    {
        _players = players;
        _rank = rank;
    }

    [SlashCommand("플레이어추가", "새 플레이어를 등록합니다.")]
    public async Task Add(
        [Summary("이름", "표시 이름(경기 기록 시 이 이름으로 지정)")] string 이름,
        [Summary("롤닉네임", "롤 인게임 닉네임")] string 롤닉네임 = "",
        [Summary("티어", "롤 티어")] string 티어 = "",
        [Summary("점수", "초기 점수(기본 100)")] int 점수 = 100)
    {
        try
        {
            var p = _players.Create(이름, 롤닉네임, 티어, 점수);
            await RespondAsync($"✅ 등록 완료: **{p.Name}** · 점수 {p.Score} · 등급 **{_rank.Resolve(p.Score).Name}**");
        }
        catch (Exception ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("플레이어목록", "등록된 플레이어와 전적을 봅니다.")]
    public async Task List()
    {
        var players = _players.GetPlayers();
        if (players.Count == 0)
        {
            await RespondAsync("등록된 플레이어가 없습니다. `/플레이어추가` 로 등록하세요.");
            return;
        }

        var lines = players.Select(p =>
        {
            var rank = _rank.Resolve(p.Score).Name;
            var nick = string.IsNullOrWhiteSpace(p.LolNickname) ? "" : $" ({p.LolNickname})";
            return $"**{p.Name}**{nick} · 점수 {p.Score} · {rank} · {p.Wins}승 {p.Losses}패 ({p.WinRate:P0})";
        });

        var embed = new EmbedBuilder()
            .WithTitle($"👥 플레이어 ({players.Count}명)")
            .WithDescription(string.Join("\n", lines))
            .WithColor(Color.Blue)
            .Build();
        await RespondAsync(embed: embed);
    }

    [SlashCommand("플레이어수정", "플레이어 정보를 수정합니다(이름으로 찾음).")]
    public async Task Edit(
        [Summary("대상", "수정할 플레이어의 현재 이름")] string 대상,
        [Summary("새이름", "비우면 유지")] string 새이름 = "",
        [Summary("롤닉네임", "비우면 유지")] string 롤닉네임 = "",
        [Summary("티어", "비우면 유지")] string 티어 = "",
        [Summary("점수", "변경할 점수(미입력 시 유지)")] int 점수 = int.MinValue)
    {
        var player = Find(대상);
        if (player is null)
        {
            await RespondAsync($"⚠️ '{대상}' 플레이어를 찾을 수 없습니다.", ephemeral: true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(새이름)) player.Name = 새이름.Trim();
        if (!string.IsNullOrWhiteSpace(롤닉네임)) player.LolNickname = 롤닉네임.Trim();
        if (!string.IsNullOrWhiteSpace(티어)) player.LolTier = 티어.Trim();
        if (점수 != int.MinValue) player.Score = 점수;

        try
        {
            _players.Update(player);
            await RespondAsync($"✅ 수정 완료: **{player.Name}** · 점수 {player.Score} · 등급 **{_rank.Resolve(player.Score).Name}**");
        }
        catch (Exception ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("플레이어삭제", "플레이어를 삭제합니다(이름으로 찾음).")]
    public async Task Delete([Summary("대상", "삭제할 플레이어 이름")] string 대상)
    {
        var player = Find(대상);
        if (player is null)
        {
            await RespondAsync($"⚠️ '{대상}' 플레이어를 찾을 수 없습니다.", ephemeral: true);
            return;
        }

        _players.Delete(player.Id);
        await RespondAsync($"🗑️ 삭제 완료: **{player.Name}**");
    }

    private Player? Find(string name)
        => _players.GetPlayers().FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
}
