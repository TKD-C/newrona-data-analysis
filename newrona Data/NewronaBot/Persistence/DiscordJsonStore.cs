using System.Text.Encodings.Web;
using System.Text.Json;
using Discord;
using Discord.WebSocket;

namespace NewronaBot.Persistence;

/// <summary>
/// 데이터를 "전용 디스코드 채널"에 JSON 파일로 저장하는 저장소.
///
/// 동작 방식:
///  - 메모리에 올라온 <see cref="NewronaDatabase"/>가 실행 중 실시간 원본(single source of truth).
///  - 시작 시 채널의 고정(pin) 메시지에서 newrona-data.json 첨부파일을 읽어 메모리로 적재.
///  - 변경이 생기면 디바운스 후 채널의 데이터 메시지를 수정(첨부 교체)하여 영속화.
///
/// 메시지 텍스트(2000자) 제한을 피하려고 파일 첨부 방식을 쓴다(경기 수백 건도 안전).
/// </summary>
public sealed class DiscordJsonStore : INewronaStore
{
    private const string FileName = "newrona-data.json";
    private const string HeaderText =
        "📦 **뉴로나 내전 데이터 저장소** — 봇이 자동 관리합니다. 이 메시지/첨부파일을 삭제하지 마세요.";

    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // 한글을 그대로 보이게
    };

    private readonly DiscordSocketClient _client;
    private readonly ulong _channelId;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private NewronaDatabase _db = new();
    private IUserMessage? _dataMessage;
    private CancellationTokenSource? _debounce;

    public DiscordJsonStore(DiscordSocketClient client, ulong channelId)
    {
        _client = client;
        _channelId = channelId;
    }

    /// <summary>채널에서 기존 데이터를 읽어 메모리에 적재(시작 시 1회).</summary>
    public async Task InitializeAsync()
    {
        var channel = GetChannel();
        var pins = await channel.GetPinnedMessagesAsync();

        foreach (var msg in pins)
        {
            if (msg.Author.Id != _client.CurrentUser.Id) continue;
            var att = msg.Attachments.FirstOrDefault(a => a.Filename == FileName);
            if (att is null) continue;

            _dataMessage = msg as IUserMessage;
            try
            {
                var json = await Http.GetStringAsync(att.Url);
                var loaded = JsonSerializer.Deserialize<NewronaDatabase>(json, JsonOpts);
                if (loaded is not null)
                {
                    lock (_lock) _db = loaded;
                    Console.WriteLine($"✅ 데이터 적재: 플레이어 {loaded.Players.Count}명, 경기 {loaded.Matches.Count}건");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ 기존 데이터 파싱 실패(빈 상태로 시작): {ex.Message}");
            }
            break;
        }

        if (_dataMessage is null)
            Console.WriteLine("ℹ️ 저장된 데이터가 없어 새로 시작합니다. 첫 변경 시 채널에 저장 메시지를 생성합니다.");
    }

    /// <summary>읽기 전용 접근(잠금 하에 스냅샷 함수 실행).</summary>
    public T Read<T>(Func<NewronaDatabase, T> read)
    {
        lock (_lock) return read(_db);
    }

    /// <summary>변경 작업(잠금 하에 실행 후 저장 예약).</summary>
    public void Mutate(Action<NewronaDatabase> mutate)
    {
        lock (_lock) mutate(_db);
        RequestSave();
    }

    /// <summary>변경을 디바운스하여 디스코드에 저장(연속 변경을 합쳐 호출 수 절감).</summary>
    private void RequestSave()
    {
        lock (_lock)
        {
            _debounce?.Cancel();
            _debounce = new CancellationTokenSource();
            var token = _debounce.Token;
            _ = DelayedSaveAsync(token);
        }
    }

    private async Task DelayedSaveAsync(CancellationToken token)
    {
        try { await Task.Delay(1200, token); }
        catch (TaskCanceledException) { return; } // 더 최근 변경이 들어와 취소됨
        await FlushAsync();
    }

    /// <summary>현재 메모리 상태를 즉시 채널에 저장(종료 시에도 호출).</summary>
    public async Task FlushAsync()
    {
        await _saveGate.WaitAsync();
        try
        {
            byte[] bytes;
            lock (_lock) bytes = JsonSerializer.SerializeToUtf8Bytes(_db, JsonOpts);

            var channel = GetChannel();
            using var ms = new MemoryStream(bytes);
            var attachment = new FileAttachment(ms, FileName);

            if (_dataMessage is null)
            {
                _dataMessage = await channel.SendFileAsync(attachment, text: HeaderText);
                try { await _dataMessage.PinAsync(); }
                catch (Exception ex) { Console.WriteLine($"⚠️ 메시지 고정 실패(권한 확인): {ex.Message}"); }
            }
            else
            {
                await _dataMessage.ModifyAsync(m =>
                {
                    m.Content = HeaderText;
                    m.Attachments = new[] { attachment };
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 데이터 저장 실패: {ex.Message}");
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private IMessageChannel GetChannel()
    {
        if (_client.GetChannel(_channelId) is IMessageChannel ch)
            return ch;
        throw new InvalidOperationException(
            $"채널({_channelId})을 찾을 수 없습니다. 봇이 해당 서버/채널에 있고 권한이 있는지 확인하세요.");
    }
}
