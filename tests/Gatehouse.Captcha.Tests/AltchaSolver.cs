using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gatehouse.Captcha.Tests;

/// <summary>Plays the browser: solves a puzzle and builds the token the widget would send.</summary>
internal static class AltchaSolver
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    /// <summary>Fast settings: a puzzle is solved in a few attempts.</summary>
    public static CaptchaOptions FastOptions(int lifetimeMinutes = 10) => new()
    {
        Provider = CaptchaProvider.Altcha,
        Altcha = new AltchaOptions { Cost = 1, MinCounter = 1, MaxCounter = 20, LifetimeMinutes = lifetimeMinutes },
    };

    /// <summary>Tries counters from 0 until the derived key starts with the key prefix.</summary>
    public static (long Counter, string DerivedKey) Solve(AltchaChallenge challenge)
    {
        var p = challenge.Parameters;
        for (long counter = 0; counter <= 1_000_000; counter++)
        {
            var key = Derive(p, counter);
            if (Convert.ToHexStringLower(key).StartsWith(p.KeyPrefix, StringComparison.Ordinal))
            {
                return (counter, Convert.ToHexStringLower(key));
            }
        }

        throw new InvalidOperationException("The puzzle could not be solved.");
    }

    /// <summary>The key for one counter: PBKDF2 of the nonce followed by the counter (4 bytes, big endian).</summary>
    public static byte[] Derive(AltchaParameters p, long counter)
    {
        var nonce = Convert.FromHexString(p.Nonce);
        var password = new byte[nonce.Length + sizeof(uint)];
        nonce.CopyTo(password, 0);
        BinaryPrimitives.WriteUInt32BigEndian(password.AsSpan(nonce.Length), (uint)counter);
        return Rfc2898DeriveBytes.Pbkdf2(
            password, Convert.FromHexString(p.Salt), p.Cost, HashAlgorithmName.SHA256, p.KeyLength);
    }

    /// <summary>The base64 JSON the widget sends: { challenge, solution: { counter, derivedKey } }.</summary>
    public static string ToToken(AltchaChallenge challenge, long counter, string derivedKey) =>
        Encode(new { challenge, solution = new { counter, derivedKey } });

    /// <summary>Solves the puzzle and builds the token.</summary>
    public static string SolveToToken(AltchaChallenge challenge)
    {
        var (counter, derivedKey) = Solve(challenge);
        return ToToken(challenge, counter, derivedKey);
    }

    public static string Encode(object payload) =>
        Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload, Web));

    public static string EncodeText(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
}
