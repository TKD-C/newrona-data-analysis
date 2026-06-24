using Discord;
using Discord.Interactions;
using NewronaBot.Riot;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaBot.Commands;

/// <summary>라이엇 API로 최근 경기를 가져와 내전(커스텀 게임)만 자동 기록하는 슬래시 명령어.</summary>
[RequireRole("내전관리자")]
public sealed class RiotMatchCommands : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IMatchService _matches;
    private readonly IPlayerService _players;
    private readonly RiotApiClient _riot;

    public RiotMatchCommands(IMatchService matches, IPlayerService players, RiotApiClient riot)
    {
        _matches = matches;
        _players = players;
        _riot = riot;
    }

    [SlashCommand("내전기록하기", "내전러의 최근 경기를 라이엇에서 가져와 내전(커스텀)만 기록합니다.")]
    public async Task Record(
        [Summary("대상", "내전러 이름(PUUID가 설정되어 있어야 함)")] string 대상,
        [Summary("개수", "확인할 최근 경기 수(기본 20, 호출량 고려 시 줄이세요)")] int 개수 = 20)
    {
        await DeferAsync();

        if (!_riot.IsEnabled)
        {
            await FollowupAsync("⚠️ 라이엇 API 키(RIOT_API_KEY)가 설정되지 않았습니다.");
            return;
        }

        var roster = _players.GetPlayers();
        var player = roster.FirstOrDefault(p => string.Equals(p.Name, 대상.Trim(), StringComparison.OrdinalIgnoreCase));
        if (player is null)
        {
            await FollowupAsync($"⚠️ '{대상}' 내전러를 찾을 수 없습니다.");
            return;
        }
        if (string.IsNullOrWhiteSpace(player.Puuid))
        {
            await FollowupAsync($"⚠️ **{player.Name}** 의 PUUID가 설정되지 않았습니다. '내전관리자'에게 `/내전러puuid설정` 을 요청하세요.");
            return;
        }

        // PUUID → 등록 내전러 매핑(참가자 식별용).
        var byPuuid = roster
            .Where(p => !string.IsNullOrWhiteSpace(p.Puuid))
            .GroupBy(p => p.Puuid)
            .ToDictionary(g => g.Key, g => g.First());

        try
        {
            개수 = Math.Clamp(개수, 1, 20); // 호출량(20req/1s, 100req/2min) 고려해 상한 20.
            var ids = await _riot.GetRecentMatchIdsAsync(player.Puuid, 개수);

            int recorded = 0, alreadyDone = 0, notCustom = 0, notFiveByFive = 0;
            var newSummaries = new List<string>();
            var scoringWarnings = new List<string>();
            var bandageNet = new Dictionary<string, int>(); // 이름 → 반창고 순변동(여러 경기 합산)
            var nameById = roster.ToDictionary(p => p.Id, p => p.Name);

            // 후보를 먼저 모은다(이미 기록/커스텀 아님/5대5 아님은 여기서 거름).
            var candidates = new List<(string Id, DateTime PlayedAt, List<MatchPlayer> Participants, Team Winner)>();
            foreach (var id in ids)
            {
                if (_matches.HasRiotMatch(id)) { alreadyDone++; continue; } // 이미 기록 → 상세 호출 생략(호출량 절약)

                var match = await _riot.GetMatchAsync(id);
                if (match is null) continue;
                if (!match.IsCustom) { notCustom++; continue; }

                var participants = match.Participants.Select(rp => new MatchPlayer
                {
                    PlayerId = byPuuid.TryGetValue(rp.Puuid, out var reg) ? reg.Id : 0,
                    PlayerName = byPuuid.TryGetValue(rp.Puuid, out var reg2) ? reg2.Name : rp.Name,
                    Puuid = rp.Puuid,
                    Team = rp.TeamId == 200 ? Team.Team2 : Team.Team1,
                    Lane = rp.Lane,
                    TeamPosition = rp.TeamPosition,
                }).ToList();

                if (participants.Count(p => p.Team == Team.Team1) != 5 ||
                    participants.Count(p => p.Team == Team.Team2) != 5)
                {
                    notFiveByFive++;
                    continue;
                }

                var winnerTeamId = match.Participants.FirstOrDefault(p => p.Win)?.TeamId ?? 100;
                var winner = winnerTeamId == 200 ? Team.Team2 : Team.Team1;

                candidates.Add((id, match.PlayedAt, participants, winner));
            }

            // Elo는 순서 의존적 → 오래된 경기부터(playedAt 오름차순) 기록해 점수를 올바른 순서로 누적.
            foreach (var c in candidates.OrderBy(c => c.PlayedAt))
            {
                var saved = _matches.RecordDetailed(c.Participants, c.Winner, c.PlayedAt, "내전 자동기록", c.Id);
                recorded++;
                if (newSummaries.Count < 5) newSummaries.Add($"`#{saved.Id}` {saved.Summary}");
                foreach (var w in saved.ScoringWarnings)
                    scoringWarnings.Add($"`#{saved.Id}` {w}");
                foreach (var (pid, delta) in saved.BandageDeltas)
                    if (nameById.TryGetValue(pid, out var nm))
                        bandageNet[nm] = bandageNet.GetValueOrDefault(nm) + delta;
            }

            var desc = $"**{player.Name}** 최근 {ids.Count}경기 확인\n" +
                       $"✅ 새로 기록: **{recorded}건**(커스텀)\n" +
                       $"↩️ 이미 기록됨: {alreadyDone}건 · 커스텀 아님: {notCustom}건" +
                       (notFiveByFive > 0 ? $" · 5대5 아님: {notFiveByFive}건" : "");
            if (newSummaries.Count > 0)
                desc += "\n\n" + string.Join("\n", newSummaries);
            if (scoringWarnings.Count > 0)
                desc += "\n\n**점수 미반영 경고**\n" + string.Join("\n", scoringWarnings.Take(5))
                      + (scoringWarnings.Count > 5 ? $"\n…외 {scoringWarnings.Count - 5}건" : "");

            var bandageChanges = bandageNet.Where(kv => kv.Value != 0)
                .OrderByDescending(kv => kv.Value)
                .Select(kv => $"{kv.Key} {(kv.Value > 0 ? "+" : "")}{kv.Value}")
                .ToList();
            if (bandageChanges.Count > 0)
                desc += "\n\n:adhesive_bandage: **반창고 변동**(주라인 −1·부/오프라인 +1)\n" + string.Join(", ", bandageChanges);

            var embed = new EmbedBuilder()
                .WithTitle("📥 내전 자동 기록")
                .WithDescription(desc)
                .WithColor(recorded > 0 ? Color.Green : Color.LightGrey)
                .WithFooter("전적/승률은 /내전러목록 에서 확인하세요.")
                .Build();
            await FollowupAsync(embed: embed);
        }
        catch (RiotApiException ex)
        {
            await FollowupAsync($"⚠️ {ex.Message}");
        }
        catch (Exception ex)
        {
            await FollowupAsync($"⚠️ 기록 중 오류: {ex.Message}");
        }
    }
}
