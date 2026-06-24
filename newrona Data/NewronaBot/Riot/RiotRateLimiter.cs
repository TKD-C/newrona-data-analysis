namespace NewronaBot.Riot;

/// <summary>
/// 라이엇 API 호출 빈도 제한기(슬라이딩 윈도우).
/// 개발용 키 기본값: 20 requests / 1s, 100 requests / 2min (라우팅 값별로 적용).
/// 모든 요청을 하나의 게이트로 직렬화하여 윈도우를 초과하면 가능 시점까지 대기시킨다.
/// </summary>
public sealed class RiotRateLimiter
{
    private readonly (int Limit, TimeSpan Window, Queue<DateTime> Hits)[] _rules;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="rules">(허용 횟수, 윈도우) 목록. 미지정 시 개발용 키 기본 한도.</param>
    public RiotRateLimiter(params (int limit, TimeSpan window)[] rules)
    {
        if (rules.Length == 0)
            rules = new[]
            {
                (20, TimeSpan.FromSeconds(1)),
                (100, TimeSpan.FromMinutes(2)),
            };
        _rules = rules.Select(r => (r.limit, r.window, new Queue<DateTime>())).ToArray();
    }

    /// <summary>호출 전 반드시 await. 모든 한도를 만족할 때까지 대기 후 호출 시각을 기록한다.</summary>
    public async Task AcquireAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            while (true)
            {
                var now = DateTime.UtcNow;
                var wait = TimeSpan.Zero;

                foreach (var rule in _rules)
                {
                    // 윈도우를 벗어난 과거 기록 제거.
                    while (rule.Hits.Count > 0 && now - rule.Hits.Peek() >= rule.Window)
                        rule.Hits.Dequeue();

                    if (rule.Hits.Count >= rule.Limit)
                    {
                        var until = rule.Hits.Peek() + rule.Window - now;
                        if (until > wait) wait = until;
                    }
                }

                if (wait <= TimeSpan.Zero) break;
                await Task.Delay(wait, ct);
            }

            var stamp = DateTime.UtcNow;
            foreach (var rule in _rules) rule.Hits.Enqueue(stamp);
        }
        finally
        {
            _gate.Release();
        }
    }
}
