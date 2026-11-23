using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Gatehouse.Captcha;

/// <summary>The puzzle parameters the challenge endpoint returns, in the altcha widget's v3 format.</summary>
/// <param name="Algorithm">The key derivation, always "PBKDF2/SHA-256".</param>
/// <param name="Nonce">Random value (hex) that starts the password.</param>
/// <param name="Salt">Random salt (hex).</param>
/// <param name="Cost">PBKDF2 iterations.</param>
/// <param name="KeyLength">Length of the derived key in bytes.</param>
/// <param name="KeyPrefix">First half of the derived key (hex) the browser has to match.</param>
/// <param name="ExpiresAt">When the puzzle expires, in Unix seconds.</param>
public sealed record AltchaParameters(
    string Algorithm,
    string Nonce,
    string Salt,
    int Cost,
    int KeyLength,
    string KeyPrefix,
    long ExpiresAt);

/// <summary>What the challenge endpoint returns: the parameters and their signature.</summary>
/// <param name="Parameters">The puzzle parameters.</param>
/// <param name="Signature">HMAC (hex) of the parameters, so the browser can't change them.</param>
public sealed record AltchaChallenge(AltchaParameters Parameters, string Signature);

/// <summary>
/// ALTCHA (self-hosted proof of work), checked here rather than with a library: there is no official .NET one, and the
/// check is short. It follows altcha-lib's v2 module (the format of the altcha 3.x widget), "deterministic mode":
/// <list type="bullet">
/// <item>The server picks a random counter, derives a key with PBKDF2(password = nonce + counter, salt), and sends the
/// first half of that key as keyPrefix. The parameters are signed with an HMAC, so the browser can't change them.</item>
/// <item>The browser tries counters from 0 until a derived key starts with keyPrefix: on average a few thousand PBKDF2
/// runs, a second or two spread over its CPU cores. A bot pays the same for every attempt.</item>
/// <item>Checking costs the server one PBKDF2 run with the counter the browser found.</item>
/// </list>
/// The HMAC key is made at startup and lives only in memory: a restart makes the open puzzles invalid, and the widget
/// fetches a new one. One app instance only: several instances would need a shared key and a shared store of used
/// puzzles.
/// </summary>
/// <param name="time">The clock used for expiry.</param>
/// <param name="options">The captcha settings; the "Altcha" part is read.</param>
public sealed class AltchaCaptcha(TimeProvider time, IOptions<CaptchaOptions> options) : ICaptchaVerifier
{
    private const string Algorithm = "PBKDF2/SHA-256";
    private const int KeyLength = 32;

    // A solved payload is a few hundred characters; anything far longer isn't one.
    private const int MaxTokenLength = 4096;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // What the widget sends back (the Captcha-Token header): base64 of this JSON.
    private sealed record Payload(AltchaChallenge? Challenge, Solution? Solution);

    private sealed record Solution(long Counter, string? DerivedKey);

    private readonly AltchaOptions _settings = options.Value.Altcha;

    // Long enough to fill in a form after ticking the box; the widget fetches a new puzzle when it expires.
    private readonly TimeSpan _lifetime = TimeSpan.FromMinutes(options.Value.Altcha.LifetimeMinutes);

    private readonly byte[] _hmacKey = RandomNumberGenerator.GetBytes(32);

    // Solved puzzles (by nonce) until they expire, so each one passes only once.
    private readonly ConcurrentDictionary<string, DateTimeOffset> _used = new();

    // UTC ticks; read and swapped with Interlocked.
    private long _nextCleanupTicks = (time.GetUtcNow() + TimeSpan.FromMinutes(options.Value.Altcha.LifetimeMinutes)).UtcTicks;

    /// <inheritdoc />
    public bool Enabled => true;

    /// <summary>Makes a new signed puzzle for the widget.</summary>
    public AltchaChallenge CreateChallenge()
    {
        var nonce = RandomNumberGenerator.GetBytes(16);
        var salt = RandomNumberGenerator.GetBytes(16);
        var counter = (uint)RandomNumberGenerator.GetInt32(_settings.MinCounter, _settings.MaxCounter + 1);
        var key = DeriveKey(nonce, salt, counter, _settings.Cost, KeyLength);

        var parameters = new AltchaParameters(
            Algorithm,
            Convert.ToHexStringLower(nonce),
            Convert.ToHexStringLower(salt),
            _settings.Cost,
            KeyLength,
            Convert.ToHexStringLower(key.AsSpan(0, KeyLength / 2)),
            (time.GetUtcNow() + _lifetime).ToUnixTimeSeconds());
        return new AltchaChallenge(parameters, Convert.ToHexStringLower(Sign(parameters)));
    }

    /// <inheritdoc />
    public Task<bool> VerifyAsync(string? token, CancellationToken ct = default) => Task.FromResult(Verify(token));

    private bool Verify(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return false;
        }

        Payload? payload;
        byte[] signature, nonce, salt, keyPrefix, submittedKey;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(Convert.FromBase64String(token), JsonOptions);
            // The widget's test mode sends { challenge: null, solution: null, test: true }: no puzzle, no pass.
            if (payload is not { Challenge: { Parameters: { } p, Signature: { } s }, Solution: { DerivedKey: { } k } })
            {
                return false;
            }

            signature = Convert.FromHexString(s);
            nonce = Convert.FromHexString(p.Nonce);
            salt = Convert.FromHexString(p.Salt);
            keyPrefix = Convert.FromHexString(p.KeyPrefix);
            submittedKey = Convert.FromHexString(k);
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentNullException)
        {
            return false;
        }

        var parameters = payload.Challenge.Parameters;
        var counter = payload.Solution.Counter;
        var now = time.GetUtcNow();

        // The signature first: after it, the parameters are known to be ours (algorithm, cost, prefix, expiry).
        if (!CryptographicOperations.FixedTimeEquals(signature, Sign(parameters))
            || parameters.ExpiresAt <= now.ToUnixTimeSeconds()
            || counter is < 0 or > uint.MaxValue)
        {
            return false;
        }

        var key = DeriveKey(nonce, salt, (uint)counter, parameters.Cost, parameters.KeyLength);
        if (!key.AsSpan().StartsWith(keyPrefix) || !CryptographicOperations.FixedTimeEquals(key, submittedKey))
        {
            return false;
        }

        RemoveExpired(now);
        if (!_used.TryAdd(parameters.Nonce, DateTimeOffset.FromUnixTimeSeconds(parameters.ExpiresAt)))
        {
            return false;
        }

        // The key derivation above takes time: a puzzle that expired meanwhile doesn't pass.
        return parameters.ExpiresAt > time.GetUtcNow().ToUnixTimeSeconds();
    }

    // altcha's password: the nonce followed by the counter as a 4-byte big-endian number ("uint32" counter mode).
    private static byte[] DeriveKey(byte[] nonce, byte[] salt, uint counter, int cost, int keyLength)
    {
        var password = new byte[nonce.Length + sizeof(uint)];
        nonce.CopyTo(password, 0);
        BinaryPrimitives.WriteUInt32BigEndian(password.AsSpan(nonce.Length), counter);
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, cost, HashAlgorithmName.SHA256, keyLength);
    }

    // HMAC-SHA256 of the parameters as JSON with the keys in alphabetical order, as altcha-lib signs them. Written
    // from the parsed values rather than taken from the browser's text, so formatting differences don't matter, and
    // JSON escaping means no value can pass for part of another.
    private byte[] Sign(AltchaParameters p)
    {
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("algorithm", p.Algorithm);
            json.WriteNumber("cost", p.Cost);
            json.WriteNumber("expiresAt", p.ExpiresAt);
            json.WriteNumber("keyLength", p.KeyLength);
            json.WriteString("keyPrefix", p.KeyPrefix);
            json.WriteString("nonce", p.Nonce);
            json.WriteString("salt", p.Salt);
            json.WriteEndObject();
        }

        return HMACSHA256.HashData(_hmacKey, buffer.ToArray());
    }

    // At most once per lifetime: puzzles that expired can't pass anyway, so they needn't be remembered.
    private void RemoveExpired(DateTimeOffset now)
    {
        var next = Interlocked.Read(ref _nextCleanupTicks);
        if (now.UtcTicks < next
            || Interlocked.CompareExchange(ref _nextCleanupTicks, (now + _lifetime).UtcTicks, next) != next)
        {
            return;
        }

        foreach (var (nonce, expiresAt) in _used)
        {
            // A minute of grace: a request that read its time before another one's cleanup can't replay the puzzle.
            if (expiresAt.AddMinutes(1) <= now)
            {
                _used.TryRemove(nonce, out _);
            }
        }
    }
}
