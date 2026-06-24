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

    [SlashCommand("팀짜주기라인", "주라인 위주로 5대5 팀을 짭니다(부족하면 부라인·반창고 우선, 그다음 Elo 균형). 이름 10명.")]
    public async Task ByLane(
        [Summary("내전러들", "참가할 10명의 이름 (예: 철수 영희 민수 ...)")] string 내전러들)
    {
        await DeferAsync();
        try
        {
            var picked = Resolve(내전러들, _players.GetPlayers());
            await FollowupAsync(embed: LaneBasedEmbed(picked));
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}");
        }
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

    // ── 라인 기반 편성(/팀짜주기라인) ────────────────────────────────
    // 5라인 고정 순서(주/부 라인 문자열과 동일한 한글 표기 — PlayerCommands.Lane enum과 일치).
    private static readonly string[] LineLanes = { "탑", "정글", "미드", "원딜", "서폿" };
    // 5명을 5라인에 배정하는 모든 순열(120개) — 한 번만 만들어 재사용.
    private static readonly List<int[]> Perms5 = BuildPermutations(5);

    /// <summary>부라인 가상 점수 감소폭(l). 균형 계산에만 쓰고 실제 저장 점수는 건드리지 않는다.</summary>
    private const int SubPenalty = 50;
    /// <summary>주·부 모두 불가해 강제 배정된 라인의 가상 점수 감소폭(3l).</summary>
    private const int ForcedPenalty = 150;

    private enum Role { Main, Sub, Forced }

    /// <summary>한 팀(5명)의 라인 배정 결과: 우선순위 지표 + 후보 배정들(균형 선택용).</summary>
    private sealed class TeamPlan
    {
        public int NonMain;     // 주라인이 아닌 인원 수(부+강제)
        public int Forced;      // 강제 배정(주·부 모두 아님) 인원 수
        public int BandageOff;  // 주라인을 못 받은 인원의 반창고 합(작을수록 좋음 → 반창고 많은 사람이 주라인)
        // 동일 우선순위를 갖는 배정 후보들: (가상점수합, 라인index→플레이어).
        public List<(double VirtualSum, Player[] ByLane)> Options = new();
    }

    /// <summary>
    /// 라인 기반 최적 편성을 찾아 임베드로 만든다. 우선순위(사전식):
    /// ① 주라인 커버 최대화(NonMain 최소) → ② 강제 배정 최소 → ③ 반창고 많은 사람 주라인(BandageOff 최소)
    /// → ④ 가상 평균 Elo 균형(부라인 −50·강제 −150 반영).
    /// </summary>
    private static Embed LaneBasedEmbed(List<Player> players)
    {
        Player[]? best1 = null, best2 = null;
        double bestVs1 = 0, bestVs2 = 0;
        int bestNonMain = 0, bestForced = 0, bestBandageOff = 0;
        double bestDiff = double.PositiveInfinity;
        bool found = false;

        // 10명을 5:5로 가르는 모든 조합(0번을 team1 고정 → 라벨 대칭 중복 제거 → 126개).
        for (int mask = 0; mask < (1 << 10); mask++)
        {
            if (System.Numerics.BitOperations.PopCount((uint)mask) != 5) continue;
            if ((mask & 1) == 0) continue;

            var team1 = new List<Player>();
            var team2 = new List<Player>();
            for (int i = 0; i < 10; i++)
                ((mask & (1 << i)) != 0 ? team1 : team2).Add(players[i]);

            // 키 ①②③은 팀별로 독립적 → 각 팀에서 사전식 최적을 따로 구해도 전체 합 최적과 일치.
            var p1 = BestPlan(team1);
            var p2 = BestPlan(team2);

            // ④ 균형: 두 팀의 최적 후보들 중 가상점수합 차이가 가장 작은 조합 선택.
            double diff = double.PositiveInfinity;
            Player[] by1 = p1.Options[0].ByLane, by2 = p2.Options[0].ByLane;
            double vs1 = p1.Options[0].VirtualSum, vs2 = p2.Options[0].VirtualSum;
            foreach (var o1 in p1.Options)
                foreach (var o2 in p2.Options)
                {
                    var d = Math.Abs(o1.VirtualSum - o2.VirtualSum);
                    if (d < diff) { diff = d; by1 = o1.ByLane; by2 = o2.ByLane; vs1 = o1.VirtualSum; vs2 = o2.VirtualSum; }
                }

            int nonMain = p1.NonMain + p2.NonMain;
            int forced = p1.Forced + p2.Forced;
            int bandageOff = p1.BandageOff + p2.BandageOff;

            if (!found || Better(nonMain, forced, bandageOff, diff,
                                  bestNonMain, bestForced, bestBandageOff, bestDiff))
            {
                found = true;
                bestNonMain = nonMain; bestForced = forced; bestBandageOff = bandageOff; bestDiff = diff;
                best1 = by1; best2 = by2; bestVs1 = vs1; bestVs2 = vs2;
            }
        }

        return LaneResultEmbed(best1!, best2!, bestVs1, bestVs2, bestNonMain, bestForced);
    }

    /// <summary>한 팀(5명)에서 사전식(①NonMain ②Forced ③BandageOff) 최적 배정과 그 동률 후보들을 구한다.</summary>
    private static TeamPlan BestPlan(List<Player> team)
    {
        TeamPlan? best = null;
        foreach (var perm in Perms5)
        {
            int nonMain = 0, forced = 0, bandageOff = 0;
            double vsum = 0;
            for (int i = 0; i < 5; i++)
            {
                var p = team[i];
                var role = RoleOf(p, LineLanes[perm[i]]);
                vsum += Virtual(p, role);
                if (role != Role.Main) { nonMain++; bandageOff += p.Bandage; }
                if (role == Role.Forced) forced++;
            }

            if (best is null)
            {
                best = new TeamPlan { NonMain = nonMain, Forced = forced, BandageOff = bandageOff };
                best.Options.Add((vsum, ByLane(team, perm)));
            }
            else
            {
                var cmp = Lex(nonMain, forced, bandageOff, best);
                if (cmp < 0)
                {
                    best = new TeamPlan { NonMain = nonMain, Forced = forced, BandageOff = bandageOff };
                    best.Options.Add((vsum, ByLane(team, perm)));
                }
                else if (cmp == 0 && best.Options.All(o => o.VirtualSum != vsum))
                {
                    best.Options.Add((vsum, ByLane(team, perm))); // 동률 후보는 가상점수합이 다를 때만 보관(균형 선택지).
                }
            }
        }
        return best!;
    }

    /// <summary>(nonMain, forced, bandageOff)를 기존 최적과 사전식 비교. 음수면 새것이 더 좋음.</summary>
    private static int Lex(int nm, int f, int bo, TeamPlan b)
    {
        if (nm != b.NonMain) return nm - b.NonMain;
        if (f != b.Forced) return f - b.Forced;
        return bo - b.BandageOff;
    }

    /// <summary>전체 편성 비용 (nonMain, forced, bandageOff, balanceDiff)의 사전식 우열.</summary>
    private static bool Better(int nm, int f, int bo, double diff,
                               int bnm, int bf, int bbo, double bdiff)
    {
        if (nm != bnm) return nm < bnm;
        if (f != bf) return f < bf;
        if (bo != bbo) return bo < bbo;
        return diff < bdiff;
    }

    private static Player[] ByLane(List<Player> team, int[] perm)
    {
        var byLane = new Player[5];
        for (int i = 0; i < 5; i++) byLane[perm[i]] = team[i];
        return byLane;
    }

    private static Role RoleOf(Player p, string lane)
        => p.MainLanes.Contains(lane) ? Role.Main
         : p.SubLanes.Contains(lane) ? Role.Sub
         : Role.Forced;

    private static double Virtual(Player p, Role role) => role switch
    {
        Role.Main => p.Score,
        Role.Sub => p.Score - SubPenalty,
        _ => p.Score - ForcedPenalty,
    };

    /// <summary>n개 원소(0..n-1)의 모든 순열.</summary>
    private static List<int[]> BuildPermutations(int n)
    {
        var result = new List<int[]>();
        var arr = Enumerable.Range(0, n).ToArray();
        void Recurse(int k)
        {
            if (k == n) { result.Add((int[])arr.Clone()); return; }
            for (int i = k; i < n; i++)
            {
                (arr[k], arr[i]) = (arr[i], arr[k]);
                Recurse(k + 1);
                (arr[k], arr[i]) = (arr[i], arr[k]);
            }
        }
        Recurse(0);
        return result;
    }

    private static Embed LaneResultEmbed(Player[] t1, Player[] t2, double vs1, double vs2, int nonMain, int forced)
    {
        var builder = new EmbedBuilder()
            .WithTitle("🧩 라인 기반 팀 편성")
            .AddField($"1팀 (가상평균 {vs1 / 5:0})", LaneTeamText(t1), inline: true)
            .AddField($"2팀 (가상평균 {vs2 / 5:0})", LaneTeamText(t2), inline: true)
            .WithColor(Color.Teal)
            .WithFooter($"가상 평균 Elo 차이: {Math.Abs(vs1 - vs2) / 5:0.#}점 · 주라인 {10 - nonMain}/10");

        var notes = new List<string>();
        if (forced > 0) notes.Add($"⚠️ 강제 배정 {forced}명(주·부 라인 모두 불가 → 가상 −150).");
        if (nonMain - forced > 0) notes.Add($"부라인 {nonMain - forced}명(가상 −50).");
        if (notes.Count > 0) builder.WithDescription(string.Join("\n", notes));

        return builder.Build();
    }

    /// <summary>팀의 라인 순서대로 한 줄씩(라인·이름·점수·역할·반창고).</summary>
    private static string LaneTeamText(Player[] byLane)
    {
        var lines = new List<string>();
        for (int l = 0; l < 5; l++)
        {
            var p = byLane[l];
            var role = RoleOf(p, LineLanes[l]);
            var tag = role switch { Role.Sub => " · 부 −50", Role.Forced => " · 강제 −150", _ => "" };
            var band = p.Bandage > 0 ? $" :adhesive_bandage:×{p.Bandage}" : "";
            lines.Add($"**{LineLanes[l]}** {p.Name} ({p.Score}{tag}){band}");
        }
        return string.Join("\n", lines);
    }

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
