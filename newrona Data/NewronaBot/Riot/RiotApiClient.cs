using System.Net;
using System.Text.Json;

namespace NewronaBot.Riot;

/// <summary>라이엇 match-v5 경기 1건의 필요한 정보만 추린 결과.</summary>
public sealed record RiotMatch(
    string MatchId,
    string GameType,
    DateTime PlayedAt,
    IReadOnlyList<RiotParticipant> Participants)
{
    public bool IsCustom => string.Equals(GameType, "CUSTOM_GAME", StringComparison.OrdinalIgnoreCase);
}

/// <summary>경기 참가자(필요한 필드만): PUUID·팀·승패·라인. <paramref name="TeamPosition"/>은 Elo 맞라인 매칭용 raw teamPosition.</summary>
public sealed record RiotParticipant(string Puuid, string Name, int TeamId, bool Win, string Lane, string TeamPosition);

/// <summary>라이엇 계정(account-v1): Riot ID(gameName#tagLine)와 PUUID.</summary>
public sealed record RiotAccount(string Puuid, string GameName, string TagLine)
{
    /// <summary>표시용 Riot ID: gameName#tagLine.</summary>
    public string RiotId => string.IsNullOrEmpty(TagLine) ? GameName : $"{GameName}#{TagLine}";
}

/// <summary>
/// 라이엇 API 호출 클라이언트(match-v5). 호출마다 <see cref="RiotRateLimiter"/>로 빈도 제한을 지킨다.
/// match-v5는 "지역 라우팅"(americas/asia/europe)을 사용 — 한국(KR)은 asia.
/// API 키 미설정 시 <see cref="IsEnabled"/>=false 로 비활성.
/// </summary>
public sealed class RiotApiClient
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly string _region;
    private readonly RiotRateLimiter _limiter;

    public RiotApiClient(string? apiKey, string region, RiotRateLimiter limiter, HttpClient? http = null)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _region = string.IsNullOrWhiteSpace(region) ? "asia" : region.Trim().ToLowerInvariant();
        _limiter = limiter;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    /// <summary>API 키가 설정되어 호출 가능한지.</summary>
    public bool IsEnabled => _apiKey is not null;

    /// <summary>
    /// Riot ID(gameName#tagLine)로 계정(account-v1)을 조회해 PUUID를 얻는다.
    /// 못 찾으면(404) null. account-v1도 match-v5와 같은 지역 라우팅(asia/americas/europe)을 쓴다.
    /// </summary>
    public async Task<RiotAccount?> GetAccountByRiotIdAsync(string gameName, string tagLine, CancellationToken ct = default)
    {
        EnsureEnabled();
        var url = $"https://{_region}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/"
                + $"{Uri.EscapeDataString(gameName)}/{Uri.EscapeDataString(tagLine)}";
        var json = await GetJsonAsync(url, ct);
        if (json is null) return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var puuid = GetStr(root, "puuid");
        if (puuid.Length == 0) return null;
        return new RiotAccount(puuid, GetStr(root, "gameName"), GetStr(root, "tagLine"));
    }

    /// <summary>최근 경기 match ID 목록(최신순). count는 1..100.</summary>
    public async Task<IReadOnlyList<string>> GetRecentMatchIdsAsync(string puuid, int count, CancellationToken ct = default)
    {
        EnsureEnabled();
        count = Math.Clamp(count, 1, 100);
        var url = $"https://{_region}.api.riotgames.com/lol/match/v5/matches/by-puuid/{Uri.EscapeDataString(puuid)}/ids?start=0&count={count}";
        var json = await GetJsonAsync(url, ct) ?? throw new RiotApiException("경기 목록을 가져오지 못했습니다(PUUID/권한 확인).");
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();
    }

    /// <summary>match ID로 경기 상세 조회. 404 등으로 못 찾으면 null.</summary>
    public async Task<RiotMatch?> GetMatchAsync(string matchId, CancellationToken ct = default)
    {
        EnsureEnabled();
        var url = $"https://{_region}.api.riotgames.com/lol/match/v5/matches/{Uri.EscapeDataString(matchId)}";
        var json = await GetJsonAsync(url, ct);
        if (json is null) return null;

        using var doc = JsonDocument.Parse(json);
        var info = doc.RootElement.GetProperty("info");
        var gameType = info.TryGetProperty("gameType", out var gt) ? gt.GetString() ?? "" : "";
        var playedAt = ResolvePlayedAt(info);

        var participants = new List<RiotParticipant>();
        if (info.TryGetProperty("participants", out var parts) && parts.ValueKind == JsonValueKind.Array)
            foreach (var p in parts.EnumerateArray())
                participants.Add(new RiotParticipant(
                    Puuid: GetStr(p, "puuid"),
                    Name: ResolveName(p),
                    TeamId: p.TryGetProperty("teamId", out var tid) ? tid.GetInt32() : 0,
                    Win: p.TryGetProperty("win", out var w) && w.ValueKind == JsonValueKind.True,
                    Lane: ResolveLane(p),
                    // Elo 맞라인 매칭은 teamPosition만 신뢰(추정 fallback 미사용 → 오염 방지).
                    TeamPosition: GetStr(p, "teamPosition")));

        return new RiotMatch(matchId, gameType, playedAt, participants);
    }

    // ── HTTP ─────────────────────────────────────────────────────
    private async Task<string?> GetJsonAsync(string url, CancellationToken ct)
    {
        await _limiter.AcquireAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Riot-Token", _apiKey);
        using var res = await _http.SendAsync(req, ct);

        if (res.StatusCode == HttpStatusCode.NotFound) return null;
        if (res.StatusCode == (HttpStatusCode)429)
            throw new RiotApiException("라이엇 API 호출 한도를 초과했습니다. 잠시 후 다시 시도하세요.");
        if (!res.IsSuccessStatusCode)
            throw new RiotApiException($"라이엇 API 오류({(int)res.StatusCode} {res.StatusCode}).");

        return await res.Content.ReadAsStringAsync(ct);
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
            throw new RiotApiException("라이엇 API 키가 설정되지 않았습니다(RIOT_API_KEY).");
    }

    // ── 파싱 헬퍼 ────────────────────────────────────────────────
    private static string GetStr(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static DateTime ResolvePlayedAt(JsonElement info)
    {
        // gameStartTimestamp / gameCreation 은 epoch milliseconds(UTC).
        foreach (var key in new[] { "gameStartTimestamp", "gameCreation", "gameEndTimestamp" })
            if (info.TryGetProperty(key, out var v) && v.TryGetInt64(out var ms) && ms > 0)
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime().DateTime;
        return DateTime.Now;
    }

    private static string ResolveName(JsonElement p)
    {
        var riotName = GetStr(p, "riotIdGameName");
        if (!string.IsNullOrWhiteSpace(riotName)) return riotName;
        return GetStr(p, "summonerName");
    }

    /// <summary>
    /// 라인 추정. 커스텀 게임은 teamPosition이 비어있는 경우가 많아 여러 후보를 순서대로 시도한다.
    /// 사용자 힌트(selectedRolePreferences)를 최우선으로, 이어서 teamPosition/individualPosition/lane.
    /// </summary>
    private static string ResolveLane(JsonElement p)
    {
        // 1) selectedRolePreferences (객체면 firstPreference, 배열이면 첫 요소, 문자열이면 그대로)
        if (p.TryGetProperty("selectedRolePreferences", out var pref))
        {
            string raw = pref.ValueKind switch
            {
                JsonValueKind.Object => pref.TryGetProperty("firstPreference", out var fp) ? fp.GetString() ?? "" : "",
                JsonValueKind.Array => pref.GetArrayLength() > 0 ? pref[0].GetString() ?? "" : "",
                JsonValueKind.String => pref.GetString() ?? "",
                _ => "",
            };
            var mapped = MapLane(raw);
            if (mapped.Length > 0) return mapped;
        }

        // 2) 표준 위치 필드들 순서대로.
        foreach (var key in new[] { "teamPosition", "individualPosition", "lane", "role" })
        {
            var mapped = MapLane(GetStr(p, key));
            if (mapped.Length > 0) return mapped;
        }
        return "";
    }

    /// <summary>라이엇 포지션 코드 → 한국어 라인. 미상/INVALID 등은 빈 문자열.</summary>
    private static string MapLane(string raw) => raw.Trim().ToUpperInvariant() switch
    {
        "TOP" => "탑",
        "JUNGLE" or "JUNGLER" => "정글",
        "MIDDLE" or "MID" => "미드",
        "BOTTOM" or "BOT" or "ADC" or "CARRY" or "DUO_CARRY" => "원딜",
        "UTILITY" or "SUPPORT" or "DUO_SUPPORT" => "서폿",
        _ => "",
    };
}

/// <summary>라이엇 API 호출 실패(사용자에게 메시지로 전달).</summary>
public sealed class RiotApiException : Exception
{
    public RiotApiException(string message) : base(message) { }
}
