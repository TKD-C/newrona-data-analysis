using Discord;
using Discord.Interactions;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaBot.Commands;

/// <summary>점수(Elo) 기반 5대5 팀 자동 편성 슬래시 명령어.</summary>
[RequireRole("내전관리자")]
public sealed class TeamCommands : InteractionModuleBase<SocketInteractionContext>
{
    private static readonly char[] Separators = { ',', ' ', '\n', '\t', '/' };

    /// <summary>랜덤 편성 시 평균 Elo 차이 허용치(이 이하면 균형으로 인정).</summary>
    private const double BalanceThreshold = 40.0;

    private static readonly Random Rng = new();

    private readonly IPlayerService _players;

    public TeamCommands(IPlayerService players)
    {
        _players = players;
    }

    [SlashCommand("팀짜주기뱀", "점수순 스네이크 드래프트로 5대5 팀을 짭니다(이름 10명, 공백/쉼표 구분).")]
    public async Task Snake(
        [Summary("내전러들", "참가할 10명의 이름 (예: 철수 영희 민수 ...)")] string 내전러들)
    {
        await RespondPicked(내전러들, SnakeSplit);
    }

    [SlashCommand("팀짜주기랜덤", "팀 평균 Elo가 비슷하도록 랜덤으로 5대5 팀을 짭니다(이름 10명).")]
    public async Task Random(
        [Summary("내전러들", "참가할 10명의 이름 (예: 철수 영희 민수 ...)")] string 내전러들)
    {
        await RespondPicked(내전러들, RandomBalancedSplit);
    }

    /// <summary>입력 파싱·검증 후 분배 전략을 적용해 결과 임베드를 응답.</summary>
    private async Task RespondPicked(string raw, Func<List<Player>, (List<Player> t1, List<Player> t2, string? warning)> splitter)
    {
        await DeferAsync();
        try
        {
            var picked = Resolve(raw, _players.GetPlayers());
            var (t1, t2, warning) = splitter(picked);
            await FollowupAsync(embed: ResultEmbed(t1, t2, warning));
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}");
        }
    }

    /// <summary>스네이크: 점수 내림차순 후 1,2,2,1,1,2,2,1,1,2 배정. 첫 사람은 50%로 팀 라벨 반전.</summary>
    private static (List<Player>, List<Player>, string?) SnakeSplit(List<Player> players)
    {
        var sorted = players.OrderByDescending(p => p.Score).ToList();
        // 인덱스(0-based) 0,3,4,7,8 → A팀 / 1,2,5,6,9 → B팀
        var pattern = new[] { 0, 1, 1, 0, 0, 1, 1, 0, 0, 1 };
        var a = new List<Player>();
        var b = new List<Player>();
        for (int i = 0; i < sorted.Count; i++)
            (pattern[i] == 0 ? a : b).Add(sorted[i]);

        // 코인플립: 멤버 구성은 동일, 1팀/2팀 라벨만 무작위로 정함(연출).
        return Rng.Next(2) == 0 ? (a, b, null) : (b, a, null);
    }

    /// <summary>랜덤 균형: 126개 5:5 조합 중 평균차 ≤40을 추려 랜덤 선택. 없으면 가장 가까운 조합+경고.</summary>
    private static (List<Player>, List<Player>, string?) RandomBalancedSplit(List<Player> players)
    {
        // C(10,5)=252이나 팀 라벨 대칭을 빼면 126개. 0번 플레이어를 항상 team1에 고정해 중복 제거.
        var combos = new List<(List<Player> t1, List<Player> t2, double diff)>();
        for (int mask = 0; mask < (1 << 10); mask++)
        {
            if (System.Numerics.BitOperations.PopCount((uint)mask) != 5) continue;
            if ((mask & 1) == 0) continue; // 0번은 항상 team1 → 대칭 중복 제거

            var t1 = new List<Player>();
            var t2 = new List<Player>();
            for (int i = 0; i < 10; i++)
                ((mask & (1 << i)) != 0 ? t1 : t2).Add(players[i]);

            var diff = Math.Abs(Avg(t1) - Avg(t2));
            combos.Add((t1, t2, diff));
        }

        var balanced = combos.Where(c => c.diff <= BalanceThreshold).ToList();
        if (balanced.Count > 0)
        {
            var pick = balanced[Rng.Next(balanced.Count)];
            return (pick.t1, pick.t2, null);
        }

        // 균형 불가: 가장 평균차 작은 조합 + 경고.
        var best = combos.OrderBy(c => c.diff).First();
        var warning = $"평균 Elo 차이 {BalanceThreshold:0}점 이하 조합이 없어, 가장 가까운 조합(차이 {best.diff:0.#}점)으로 편성했습니다.";
        return (best.t1, best.t2, warning);
    }

    private static double Avg(List<Player> team) => team.Average(p => p.Score);

    /// <summary>팀 편성 결과 임베드(팀별 멤버·점수, 평균 Elo, 평균차).</summary>
    private static Embed ResultEmbed(List<Player> t1, List<Player> t2, string? warning)
    {
        double a1 = Avg(t1), a2 = Avg(t2);
        var builder = new EmbedBuilder()
            .WithTitle("⚔️ 팀 편성 결과")
            .AddField($"1팀 (평균 {a1:0})", TeamText(t1), inline: true)
            .AddField($"2팀 (평균 {a2:0})", TeamText(t2), inline: true)
            .WithColor(Color.Blue)
            .WithFooter($"평균 Elo 차이: {Math.Abs(a1 - a2):0.#}점");

        if (warning is not null)
            builder.WithDescription($"⚠️ {warning}");

        return builder.Build();
    }

    private static string TeamText(List<Player> team) =>
        string.Join("\n", team.OrderByDescending(p => p.Score).Select(p => $"{p.Name} ({p.Score})"));

    /// <summary>입력을 플레이어로 변환. 미등록·중복·인원수(정확히 10명) 검증.</summary>
    private static List<Player> Resolve(string raw, IReadOnlyList<Player> roster)
    {
        var names = raw.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var picked = new List<Player>();
        var missing = new List<string>();
        var seen = new HashSet<int>();
        var duplicate = new List<string>();

        foreach (var name in names)
        {
            var hit = roster.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (hit is null) { missing.Add(name); continue; }
            if (!seen.Add(hit.Id)) { duplicate.Add(name); continue; }
            picked.Add(hit);
        }

        if (missing.Count > 0)
            throw new InvalidOperationException($"등록되지 않은 내전러: {string.Join(", ", missing)} — 먼저 /내전러추가 로 등록하세요.");
        if (duplicate.Count > 0)
            throw new InvalidOperationException($"중복된 내전러: {string.Join(", ", duplicate.Distinct())}");
        if (picked.Count != 10)
            throw new InvalidOperationException($"정확히 10명을 입력하세요. 현재 {picked.Count}명.");

        return picked;
    }
}
