using System.Security.Cryptography;
using System.Text;

namespace CodeDotNet.Ai.Security;

public sealed record PairResult(bool Success, string? Token, DateTimeOffset? ExpiresAt, bool CodeExpiredOrLocked);

/// <summary>
/// Exchanges a short pairing code (printed on the service console, reusable for its 30 minute
/// lifetime) for a bearer session token. Failed attempts are limited; only token hashes are kept.
/// </summary>
public sealed class PairingService(TimeProvider time, ILogger<PairingService> logger)
{
    public const int MaxFailedAttempts = 5;
    public const int MaxSessions = 16;
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    private readonly object _lock = new();
    private readonly Dictionary<string, DateTimeOffset> _sessions = new(StringComparer.Ordinal);
    private string? _code;
    private DateTimeOffset _codeExpires;
    private int _failures;
    private bool _issued;

    /// <summary>Returns the active pairing code, creating one if none is active.</summary>
    public string CurrentCode
    {
        get
        {
            lock (_lock)
            {
                if (_code is null || time.GetUtcNow() >= _codeExpires)
                {
                    GenerateCode();
                }

                return _code!;
            }
        }
    }

    public void Rotate(string reason)
    {
        lock (_lock)
        {
            GenerateCode();
            logger.LogWarning("Pairing code rotated ({Reason}). New code: {Code}", reason, _code);
        }
    }

    /// <summary>Issues and logs a fresh code when the active one has expired. Returns true when it rotated.</summary>
    public bool RotateIfExpired()
    {
        lock (_lock)
        {
            if (_code is not null && time.GetUtcNow() < _codeExpires)
            {
                return false;
            }

            GenerateCode();
            logger.LogWarning("Pairing code expired. New code: {Code}", _code);
            return true;
        }
    }

    public PairResult TryPair(string? candidate)
    {
        lock (_lock)
        {
            var now = time.GetUtcNow();

            if (!_issued)
            {
                GenerateCode();
            }

            if (_code is null)
            {
                return new PairResult(false, null, null, true);
            }

            if (now >= _codeExpires)
            {
                GenerateCode();
                logger.LogWarning("Pairing code expired. New code: {Code}", _code);
                return new PairResult(false, null, null, true);
            }

            if (!FixedTimeEquals(candidate, _code))
            {
                _failures++;
                if (_failures >= MaxFailedAttempts)
                {
                    GenerateCode();
                    logger.LogWarning("Pairing code locked after too many failed attempts. New code: {Code}", _code);
                    return new PairResult(false, null, null, true);
                }

                return new PairResult(false, null, null, false);
            }

            // The code stays valid (reusable) until it expires; each pairing gets its own session token.
            _failures = 0;
            PurgeExpired(now);
            while (_sessions.Count >= MaxSessions)
            {
                var oldest = _sessions.MinBy(kv => kv.Value).Key;
                _sessions.Remove(oldest);
            }

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var expires = now + SessionLifetime;
            _sessions[Hash(token)] = expires;
            return new PairResult(true, token, expires, false);
        }
    }

    public bool ValidateToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        lock (_lock)
        {
            var now = time.GetUtcNow();
            PurgeExpired(now);
            return _sessions.TryGetValue(Hash(token), out var expires) && expires > now;
        }
    }

    public int ActiveSessionCount
    {
        get
        {
            lock (_lock)
            {
                PurgeExpired(time.GetUtcNow());
                return _sessions.Count;
            }
        }
    }

    private void GenerateCode()
    {
        var chars = new char[CodeLength];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        _code = new string(chars);
        _issued = true;
        _codeExpires = time.GetUtcNow() + CodeLifetime;
        _failures = 0;
    }

    private void PurgeExpired(DateTimeOffset now)
    {
        foreach (var key in _sessions.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList())
        {
            _sessions.Remove(key);
        }
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool FixedTimeEquals(string? a, string b)
    {
        if (a is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a.Trim().ToUpperInvariant()),
            Encoding.UTF8.GetBytes(b));
    }
}
