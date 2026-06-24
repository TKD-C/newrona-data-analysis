using NewronaBot.Persistence;
using NewronaData.Data;
using NewronaData.Models;

namespace NewronaBot.Data;

/// <summary>
/// <see cref="INewronaStore"/>(디스코드 채널/파이어스토어 등) 기반 플레이어 저장소.
/// 기존 SQLite 구현과 동일한 계약(IPlayerRepository)을 만족하므로 서비스 계층은 그대로 재사용된다.
/// 승/패 통계는 (예전 SQL JOIN처럼) 경기 데이터로부터 메모리에서 집계한다.
/// </summary>
public sealed class DiscordPlayerRepository : IPlayerRepository
{
    private readonly INewronaStore _store;
    public DiscordPlayerRepository(INewronaStore store) => _store = store;

    public IReadOnlyList<Player> GetAll() => _store.Read(db =>
    {
        // 플레이어별 승/패 집계: 참가한 경기에서 본인 팀 == 승리 팀이면 승, 아니면 패.
        var wins = new Dictionary<int, int>();
        var losses = new Dictionary<int, int>();
        foreach (var m in db.Matches)
            foreach (var p in m.Participants)
            {
                var dict = p.Team == m.Winner ? wins : losses;
                dict[p.PlayerId] = dict.GetValueOrDefault(p.PlayerId) + 1;
            }

        return db.Players
            .Select(r => new Player
            {
                Id = r.Id,
                Name = r.Name,
                LolNickname = r.LolNickname,
                MainLanes = new List<string>(r.MainLanes),
                SubLanes = new List<string>(r.SubLanes),
                Puuid = r.Puuid,
                Score = r.Score,
                Wins = wins.GetValueOrDefault(r.Id),
                Losses = losses.GetValueOrDefault(r.Id),
            })
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Name)
            .ToList();
    });

    public Player Add(Player player)
    {
        _store.Mutate(db =>
        {
            player.Id = db.NextPlayerId++;
            db.Players.Add(new PlayerRecord
            {
                Id = player.Id,
                Name = player.Name,
                LolNickname = player.LolNickname,
                MainLanes = new List<string>(player.MainLanes),
                SubLanes = new List<string>(player.SubLanes),
                Puuid = player.Puuid,
                Score = player.Score,
            });
        });
        return player;
    }

    public void Update(Player player) => _store.Mutate(db =>
    {
        var rec = db.Players.FirstOrDefault(p => p.Id == player.Id);
        if (rec is null) return;
        rec.Name = player.Name;
        rec.LolNickname = player.LolNickname;
        rec.MainLanes = new List<string>(player.MainLanes);
        rec.SubLanes = new List<string>(player.SubLanes);
        rec.Puuid = player.Puuid;
        rec.Score = player.Score;
    });

    public void Delete(int playerId) => _store.Mutate(db =>
    {
        db.Players.RemoveAll(p => p.Id == playerId);
        // SQLite FK CASCADE와 동일하게, 해당 플레이어의 경기 참여 기록도 제거.
        foreach (var m in db.Matches)
            m.Participants.RemoveAll(p => p.PlayerId == playerId);
    });
}
