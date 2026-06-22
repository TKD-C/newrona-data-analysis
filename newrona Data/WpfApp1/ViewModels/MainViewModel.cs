namespace NewronaData.ViewModels;

/// <summary>탭 구성 + 자식 VM 간 변경 전파 조정.</summary>
public sealed class MainViewModel : ViewModelBase
{
    public PlayerListViewModel Players { get; }
    public MatchViewModel Matches { get; }
    public RankViewModel Ranks { get; }

    public MainViewModel(PlayerListViewModel players, MatchViewModel matches, RankViewModel ranks)
    {
        Players = players;
        Matches = matches;
        Ranks = ranks;

        // 플레이어 변경 → 경기 입력 목록/등급 갱신
        Players.Changed += () => { Matches.Refresh(); Ranks.Refresh(); };
        // 경기 변경 → 승패 통계/등급 갱신
        Matches.Changed += () => { Players.Refresh(); Ranks.Refresh(); };
    }
}
