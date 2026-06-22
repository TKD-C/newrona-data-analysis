using System.Collections.ObjectModel;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaData.ViewModels;

/// <summary>서버 내 롤 등급별 정렬 표시.</summary>
public sealed class RankViewModel : ViewModelBase
{
    private readonly IPlayerService _playerService;
    private readonly IRankService _rankService;

    public ObservableCollection<RankGroup> Groups { get; } = new();

    public RankViewModel(IPlayerService playerService, IRankService rankService)
    {
        _playerService = playerService;
        _rankService = rankService;
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public RelayCommand RefreshCommand { get; }

    public void Refresh()
    {
        Groups.Clear();
        foreach (var g in _rankService.Group(_playerService.GetPlayers()))
            Groups.Add(g);
    }
}
