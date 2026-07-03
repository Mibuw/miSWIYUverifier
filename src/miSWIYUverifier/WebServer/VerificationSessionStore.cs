using System.Collections.Concurrent;
using miSWIYUverifier.Models;

namespace miSWIYUverifier.WebServer;

/// <summary>
/// A single verification started via the public REST API.
/// Unlike <see cref="AppState"/> (which backs the single-page demo UI),
/// many sessions can run in parallel.
/// </summary>
public class VerificationSession
{
    /// <summary>Verification-ID des swiyu-verifier (zugleich Session-ID).</summary>
    public required string Id { get; init; }

    public required string DeepLink { get; init; }
    public required byte[] QrPng { get; init; }

    /// <summary>waiting | complete | partial | error</summary>
    public string Status { get; set; } = "waiting";

    public IdentityData? Identity { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>Letzter roher JSON-Response der Management-API (für /api/debug).</summary>
    public string? LastRawResponse { get; set; }

    public DateTime CreatedUtc { get; } = DateTime.UtcNow;
}

/// <summary>
/// Thread-safe in-memory store for API verification sessions.
/// Sessions expire automatically — the verifier deletes verifications after
/// VERIFICATION_TTL_SEC (default 900 s) anyway, so keeping them longer is pointless.
/// </summary>
public class VerificationSessionStore
{
    private readonly ConcurrentDictionary<string, VerificationSession> _sessions = new();
    private readonly TimeSpan _lifetime;

    public VerificationSessionStore(TimeSpan? lifetime = null)
        => _lifetime = lifetime ?? TimeSpan.FromMinutes(30);

    public void Add(VerificationSession session)
    {
        RemoveExpired();
        _sessions[session.Id] = session;
    }

    public VerificationSession? Get(string id)
    {
        RemoveExpired();
        return _sessions.TryGetValue(id, out var session) ? session : null;
    }

    public bool Remove(string id) => _sessions.TryRemove(id, out _);

    private void RemoveExpired()
    {
        var cutoff = DateTime.UtcNow - _lifetime;
        foreach (var (id, session) in _sessions)
            if (session.CreatedUtc < cutoff)
                _sessions.TryRemove(id, out _);
    }
}
