using System.IO;
using System.Windows;
using NewronaData.Data;
using NewronaData.Services;
using NewronaData.ViewModels;
using NewronaData.Views;

namespace NewronaData;

/// <summary>
/// 합성 루트(Composition Root). 의존성을 한 곳에서 조립하여 DIP를 만족.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "newrona.db");

        IDbConnectionFactory factory = new SqliteConnectionFactory(dbPath);
        new DatabaseInitializer(factory).Initialize();

        IPlayerRepository playerRepo = new PlayerRepository(factory);
        IMatchRepository matchRepo = new MatchRepository(factory);

        IRankService rankService = new RankService();
        IScoringStrategy scoring = new NoOpScoringStrategy(); // 점수 시스템 추후 교체
        IPlayerService playerService = new PlayerService(playerRepo);
        IMatchService matchService = new MatchService(matchRepo, playerRepo, scoring);

        var main = new MainViewModel(
            new PlayerListViewModel(playerService, rankService),
            new MatchViewModel(matchService, playerService),
            new RankViewModel(playerService, rankService));

        new MainWindow { DataContext = main }.Show();
    }
}
