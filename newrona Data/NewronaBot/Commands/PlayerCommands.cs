using Discord;
using Discord.Interactions;
using NewronaBot.Riot;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaBot.Commands;

/// <summary>라인(포지션) 선택지. 슬래시 명령에서 드롭다운으로 노출된다.</summary>
public enum Lane
{
    탑,
    정글,
    미드,
    원딜,
    서폿,
}

/// <summary>내전러(플레이어) 관리 슬래시 명령어.</summary>
public sealed class PlayerCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IPlayerService _players;
    private readonly IRankService _rank;
    private readonly RiotApiClient _riot;

    public PlayerCommands(IPlayerService players, IRankService rank, RiotApiClient riot)
    {
        _players = players;
        _rank = rank;
        _riot = riot;
    }

    [RequireRole("내전관리자")]
    [SlashCommand("내전러추가", "새 내전러를 등록합니다.")]
    public async Task Add(
        [Summary("이름", "표시 이름(경기 기록 시 이 이름으로 지정)")] string 이름,
        [Summary("롤닉네임", "Riot ID를 'AAA#BB' 형식으로 입력 (PUUID 자동 조회)")] string 롤닉네임 = "",
        [Summary("주라인1", "주 포지션 (최대 2개)")] Lane? 주라인1 = null,
        [Summary("주라인2", "주 포지션 2")] Lane? 주라인2 = null,
        [Summary("부라인1", "부 포지션 (최대 2개)")] Lane? 부라인1 = null,
        [Summary("부라인2", "부 포지션 2")] Lane? 부라인2 = null,
        [Summary("점수", "초기 점수(기본 1000)")] int 점수 = 1000)
    {
        await DeferAsync();
        try
        {
            // 롤닉네임이 'AAA#BB' 형식이면 #으로 나눠 Riot ID로 PUUID를 조회한다.
            var (account, puuidNote) = await ResolveAccountAsync(롤닉네임);
            var 닉네임표시 = account?.RiotId ?? 롤닉네임;

            var p = _players.Create(이름, 닉네임표시, 점수, Lanes(주라인1, 주라인2), Lanes(부라인1, 부라인2));

            // PUUID를 찾았으면 저장(비밀 정보지만 등록 단계에서 자동 연결).
            if (account is not null)
            {
                p.Puuid = account.Puuid;
                _players.Update(p);
            }

            await FollowupAsync($"✅ 등록 완료: **{p.Name}** · 점수 {p.Score} · 등급 {_rank.Resolve(p.Score).Name}{puuidNote}");
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}");
        }
    }

    /// <summary>
    /// 'AAA#BB' 형식 입력을 gameName/tagLine으로 나눠 account-v1로 PUUID를 조회한다.
    /// 반환: (조회된 계정 또는 null, 사용자에게 덧붙일 안내문구).
    /// 빈 입력·# 없음·API 비활성·조회 실패 시 account=null로 등록은 계속 진행한다.
    /// </summary>
    private async Task<(RiotAccount? account, string note)> ResolveAccountAsync(string 롤닉네임)
    {
        var raw = 롤닉네임.Trim();
        if (raw.Length == 0) return (null, "");

        var hash = raw.IndexOf('#');
        if (hash < 0)
            return (null, "\n⚠️ Riot ID는 `AAA#BB` 형식으로 입력해야 PUUID가 자동 연결됩니다.");

        var gameName = raw[..hash].Trim();
        var tagLine = raw[(hash + 1)..].Trim();
        if (gameName.Length == 0 || tagLine.Length == 0)
            return (null, "\n⚠️ Riot ID 형식이 올바르지 않습니다(`AAA#BB`).");

        if (!_riot.IsEnabled)
            return (null, "\nℹ️ 라이엇 API 키 미설정 — 닉네임만 저장(PUUID 미연결).");

        try
        {
            var account = await _riot.GetAccountByRiotIdAsync(gameName, tagLine);
            return account is null
                ? (null, $"\n⚠️ Riot ID `{gameName}#{tagLine}` 를 찾지 못했습니다(닉네임만 저장).")
                : (account, "\n🔑 PUUID 자동 연결 완료.");
        }
        catch (RiotApiException ex)
        {
            return (null, $"\n⚠️ PUUID 조회 실패: {ex.Message} (닉네임만 저장)");
        }
    }

    [SlashCommand("내전러목록", "등록된 내전러와 전적을 봅니다.")]
    public async Task List()
    {
        var players = _players.GetPlayers();
        if (players.Count == 0)
        {
            await RespondAsync("등록된 내전러가 없습니다. `/내전러추가` 로 등록하세요.");
            return;
        }

        var lines = players.Select(p =>
        {
            var rank = _rank.Resolve(p.Score).Name;
            var nick = string.IsNullOrWhiteSpace(p.LolNickname) ? "" : $" ({p.LolNickname})";
            var lanes = LanesText(p);
            return $"**{p.Name}**{nick} · 점수 {p.Score} · {rank}{lanes} · {p.Wins}승 {p.Losses}패 ({p.WinRate:P0})";
        });

        var embed = new EmbedBuilder()
            .WithTitle($"👥 내전러 ({players.Count}명)")
            .WithDescription(string.Join("\n", lines))
            .WithColor(Color.Blue)
            .Build();
        await RespondAsync(embed: embed);
    }

    [RequireRole("내전관리자")]
    [SlashCommand("내전러수정", "내전러 정보를 수정합니다(이름으로 찾음).")]
    public async Task Edit(
        [Summary("대상", "수정할 내전러의 현재 이름")] string 대상,
        [Summary("새이름", "비우면 유지")] string 새이름 = "",
        [Summary("롤닉네임", "비우면 유지")] string 롤닉네임 = "",
        [Summary("주라인1", "지정 시 주 라인을 교체 (미지정 시 유지)")] Lane? 주라인1 = null,
        [Summary("주라인2", "주 포지션 2")] Lane? 주라인2 = null,
        [Summary("부라인1", "지정 시 부 라인을 교체 (미지정 시 유지)")] Lane? 부라인1 = null,
        [Summary("부라인2", "부 포지션 2")] Lane? 부라인2 = null,
        [Summary("점수", "변경할 점수(미입력 시 유지)")] int 점수 = int.MinValue)
    {
        var player = Find(대상);
        if (player is null)
        {
            await RespondAsync($"⚠️ '{대상}' 내전러를 찾을 수 없습니다.", ephemeral: true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(새이름)) player.Name = 새이름.Trim();
        if (!string.IsNullOrWhiteSpace(롤닉네임)) player.LolNickname = 롤닉네임.Trim();
        var main = Lanes(주라인1, 주라인2);
        if (main.Count > 0) player.MainLanes = main;
        var sub = Lanes(부라인1, 부라인2);
        if (sub.Count > 0) player.SubLanes = sub;
        if (점수 != int.MinValue) player.Score = 점수;

        try
        {
            _players.Update(player);
            await RespondAsync($"✅ 수정 완료: **{player.Name}** · 점수 {player.Score} · 등급 {_rank.Resolve(player.Score).Name}");
        }
        catch (Exception ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
    }

    [RequireRole("내전관리자")]
    [SlashCommand("내전러삭제", "내전러를 삭제합니다(이름으로 찾음).")]
    public async Task Delete([Summary("대상", "삭제할 내전러 이름")] string 대상)
    {
        var player = Find(대상);
        if (player is null)
        {
            await RespondAsync($"⚠️ '{대상}' 내전러를 찾을 수 없습니다.", ephemeral: true);
            return;
        }

        _players.Delete(player.Id);
        await RespondAsync($"🗑️ 삭제 완료: **{player.Name}**");
    }

    // ── 비밀 정보(PUUID): '내전관리자'만 설정/조회, 응답은 본인에게만(ephemeral) ──
    [RequireRole("내전관리자")]
    [SlashCommand("내전러puuid설정", "[내전관리자] 내전러의 PUUID(비밀)를 설정합니다.")]
    public async Task SetPuuid(
        [Summary("대상", "내전러 이름")] string 대상,
        [Summary("puuid", "라이엇 PUUID")] string puuid)
    {
        var player = Find(대상);
        if (player is null)
        {
            await RespondAsync($"⚠️ '{대상}' 내전러를 찾을 수 없습니다.", ephemeral: true);
            return;
        }

        player.Puuid = puuid.Trim();
        try
        {
            _players.Update(player);
            await RespondAsync($"🔑 **{player.Name}** PUUID를 설정했습니다.", ephemeral: true);
        }
        catch (Exception ex)
        {
            await RespondAsync($"⚠️ {ex.Message}", ephemeral: true);
        }
    }

    [RequireRole("내전관리자")]
    [SlashCommand("내전러puuid조회", "[내전관리자] 내전러의 PUUID(비밀)를 조회합니다.")]
    public async Task GetPuuid([Summary("대상", "내전러 이름")] string 대상)
    {
        var player = Find(대상);
        if (player is null)
        {
            await RespondAsync($"⚠️ '{대상}' 내전러를 찾을 수 없습니다.", ephemeral: true);
            return;
        }

        var value = string.IsNullOrWhiteSpace(player.Puuid) ? "_(설정되지 않음)_" : $"`{player.Puuid}`";
        await RespondAsync($"🔑 **{player.Name}** PUUID: {value}", ephemeral: true);
    }

    private Player? Find(string name)
        => _players.GetPlayers().FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>선택된 라인들을 문자열 목록으로(중복/빈 값 제거, 서비스에서 최대 2개로 제한).</summary>
    private static List<string> Lanes(params Lane?[] lanes)
        => lanes.Where(l => l.HasValue).Select(l => l!.Value.ToString()).Distinct().ToList();

    private static string LanesText(Player p)
    {
        var parts = new List<string>();
        if (p.MainLanes.Count > 0) parts.Add($"주 {string.Join("/", p.MainLanes)}");
        if (p.SubLanes.Count > 0) parts.Add($"부 {string.Join("/", p.SubLanes)}");
        return parts.Count == 0 ? "" : " · " + string.Join(" ", parts);
    }
}
