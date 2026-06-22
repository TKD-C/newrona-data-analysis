using System.Collections.ObjectModel;
using System.Windows;
using NewronaData.Models;
using NewronaData.Services;

namespace NewronaData.ViewModels;

/// <summary>플레이어 추가/수정/삭제 + 목록 표시.</summary>
public sealed class PlayerListViewModel : ViewModelBase
{
    private readonly IPlayerService _service;
    private readonly IRankService _rank;

    public ObservableCollection<Player> Players { get; } = new();

    public event Action? Changed;

    public PlayerListViewModel(IPlayerService service, IRankService rank)
    {
        _service = service;
        _rank = rank;
        AddCommand = new RelayCommand(Add);
        UpdateCommand = new RelayCommand(Update, () => SelectedPlayer != null);
        DeleteCommand = new RelayCommand(Delete, () => SelectedPlayer != null);
        NewCommand = new RelayCommand(ClearForm);
        Refresh();
    }

    public RelayCommand AddCommand { get; }
    public RelayCommand UpdateCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand NewCommand { get; }

    private Player? _selectedPlayer;
    public Player? SelectedPlayer
    {
        get => _selectedPlayer;
        set
        {
            if (SetProperty(ref _selectedPlayer, value) && value != null)
            {
                Name = value.Name;
                Nickname = value.LolNickname;
                Tier = value.LolTier;
                Score = value.Score;
            }
        }
    }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _nickname = "";
    public string Nickname { get => _nickname; set => SetProperty(ref _nickname, value); }

    private string _tier = "";
    public string Tier { get => _tier; set => SetProperty(ref _tier, value); }

    private int _score = 100;
    public int Score { get => _score; set { if (SetProperty(ref _score, value)) OnPropertyChanged(nameof(RankPreview)); } }

    public string RankPreview => _rank.Resolve(_score).Name;

    public void Refresh()
    {
        Players.Clear();
        foreach (var p in _service.GetPlayers()) Players.Add(p);
    }

    private void Add()
    {
        try
        {
            _service.Create(Name, Nickname, Tier, Score);
            ClearForm();
            Refresh();
            Changed?.Invoke();
        }
        catch (Exception ex) { Warn(ex); }
    }

    private void Update()
    {
        if (SelectedPlayer is null) return;
        try
        {
            SelectedPlayer.Name = Name;
            SelectedPlayer.LolNickname = Nickname;
            SelectedPlayer.LolTier = Tier;
            SelectedPlayer.Score = Score;
            _service.Update(SelectedPlayer);
            Refresh();
            Changed?.Invoke();
        }
        catch (Exception ex) { Warn(ex); }
    }

    private void Delete()
    {
        if (SelectedPlayer is null) return;
        _service.Delete(SelectedPlayer.Id);
        ClearForm();
        Refresh();
        Changed?.Invoke();
    }

    private void ClearForm()
    {
        SelectedPlayer = null;
        Name = Nickname = Tier = "";
        Score = 100;
        OnPropertyChanged(nameof(RankPreview));
    }

    private static void Warn(Exception ex)
        => MessageBox.Show(ex.Message, "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
}
