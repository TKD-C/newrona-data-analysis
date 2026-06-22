using System.Collections.ObjectModel;
using System.Windows;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaData.ViewModels;

/// <summary>한 팀의 한 자리. 선택 가능한 전체 플레이어 목록을 공유 참조.</summary>
public sealed class PlayerSlot : ViewModelBase
{
    public ObservableCollection<Player> Available { get; }
    public PlayerSlot(ObservableCollection<Player> available) => Available = available;

    private Player? _selected;
    public Player? Selected { get => _selected; set => SetProperty(ref _selected, value); }
}

/// <summary>5대5 경기 수동 입력 + 기록 목록.</summary>
public sealed class MatchViewModel : ViewModelBase
{
    private const int TeamSize = 5;

    private readonly IMatchService _matchService;
    private readonly IPlayerService _playerService;

    public ObservableCollection<Player> AllPlayers { get; } = new();
    public ObservableCollection<PlayerSlot> Team1 { get; } = new();
    public ObservableCollection<PlayerSlot> Team2 { get; } = new();
    public ObservableCollection<Match> Matches { get; } = new();

    public event Action? Changed;

    public MatchViewModel(IMatchService matchService, IPlayerService playerService)
    {
        _matchService = matchService;
        _playerService = playerService;
        for (int i = 0; i < TeamSize; i++)
        {
            Team1.Add(new PlayerSlot(AllPlayers));
            Team2.Add(new PlayerSlot(AllPlayers));
        }
        SaveCommand = new RelayCommand(Save);
        DeleteCommand = new RelayCommand(Delete, () => SelectedMatch != null);
        Refresh();
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand DeleteCommand { get; }

    private bool _team1Wins = true;
    public bool Team1Wins { get => _team1Wins; set => SetProperty(ref _team1Wins, value); }

    private DateTime _playedAt = DateTime.Now;
    public DateTime PlayedAt { get => _playedAt; set => SetProperty(ref _playedAt, value); }

    private string _note = "";
    public string Note { get => _note; set => SetProperty(ref _note, value); }

    private Match? _selectedMatch;
    public Match? SelectedMatch { get => _selectedMatch; set => SetProperty(ref _selectedMatch, value); }

    public void Refresh()
    {
        var sel1 = Team1.Select(s => s.Selected?.Id).ToList();
        var sel2 = Team2.Select(s => s.Selected?.Id).ToList();

        AllPlayers.Clear();
        foreach (var p in _playerService.GetPlayers()) AllPlayers.Add(p);

        // 선택 복원
        for (int i = 0; i < TeamSize; i++)
        {
            Team1[i].Selected = AllPlayers.FirstOrDefault(p => p.Id == sel1[i]);
            Team2[i].Selected = AllPlayers.FirstOrDefault(p => p.Id == sel2[i]);
        }

        Matches.Clear();
        foreach (var m in _matchService.GetMatches()) Matches.Add(m);
    }

    private void Save()
    {
        try
        {
            var t1 = Ids(Team1);
            var t2 = Ids(Team2);
            var winner = Team1Wins ? Team.Team1 : Team.Team2;
            _matchService.Record(t1, t2, winner, PlayedAt, Note);
            ResetForm();
            Refresh();
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "경기 저장 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Delete()
    {
        if (SelectedMatch is null) return;
        _matchService.Delete(SelectedMatch.Id);
        Refresh();
        Changed?.Invoke();
    }

    private static List<int> Ids(IEnumerable<PlayerSlot> team)
        => team.Select(s => s.Selected?.Id
            ?? throw new InvalidOperationException("모든 자리에 플레이어를 선택하세요.")).ToList();

    private void ResetForm()
    {
        foreach (var s in Team1) s.Selected = null;
        foreach (var s in Team2) s.Selected = null;
        Note = "";
        Team1Wins = true;
        PlayedAt = DateTime.Now;
    }
}
