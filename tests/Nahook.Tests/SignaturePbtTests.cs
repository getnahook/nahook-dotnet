using System;
using System.Security.Cryptography;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace Nahook.Tests;

/// <summary>
/// Property-based tests for webhook signature sign/verify using FsCheck.
/// </summary>
public sealed class SignaturePbtTests
{
    private static string Sign(string secret, string msgId, string timestamp, string payload)
    {
        var rawSecret = secret.StartsWith("whsec_") ? secret[6..] : secret;
        byte[] key;
        try
        {
            key = Convert.FromBase64String(rawSecret);
        }
        catch (FormatException)
        {
            key = Encoding.UTF8.GetBytes(rawSecret);
        }

        var toSign = $"{msgId}.{timestamp}.{payload}";
        using var hmac = new HMACSHA256(key);
        var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign));
        return $"v1,{Convert.ToBase64String(digest)}";
    }

    private static bool Verify(string secret, string msgId, string timestamp, string payload, string signature)
    {
        var expected = Sign(secret, msgId, timestamp, payload);
        return string.Equals(expected, signature, StringComparison.Ordinal);
    }

    /// <summary>
    /// Generate a non-null, non-empty string for use as secret/msgId/payload.
    /// Uses a fixed base64-safe secret to avoid HMAC key issues.
    /// </summary>
    private static string MakeSecret(int seed)
    {
        // Create a deterministic 32-byte key and base64-encode it
        var bytes = new byte[32];
        var rng = new System.Random(seed);
        rng.NextBytes(bytes);
        return "whsec_" + Convert.ToBase64String(bytes);
    }

    // Property 1: sign then verify roundtrip always succeeds
    [Property(MaxTest = 100)]
    public Property SignThenVerify_roundtrip(PositiveInt seedVal, NonEmptyString msgId, NonEmptyString payload)
    {
        var secret = MakeSecret(seedVal.Get);
        var timestamp = "1700000000";
        var sig = Sign(secret, msgId.Get, timestamp, payload.Get);
        var result = Verify(secret, msgId.Get, timestamp, payload.Get, sig);
        return result.ToProperty();
    }

    // Property 2: tampered payload always fails verification
    [Property(MaxTest = 100)]
    public Property TamperedPayload_fails(PositiveInt seedVal, NonEmptyString msgId, NonEmptyString payload)
    {
        var secret = MakeSecret(seedVal.Get);
        var timestamp = "1700000000";
        var sig = Sign(secret, msgId.Get, timestamp, payload.Get);
        var tampered = payload.Get + "_tampered";
        var result = Verify(secret, msgId.Get, timestamp, tampered, sig);
        return (!result).ToProperty();
    }

    // Property 3: wrong secret always fails verification
    [Property(MaxTest = 100)]
    public Property WrongSecret_fails(PositiveInt seed1, PositiveInt seed2, NonEmptyString msgId, NonEmptyString payload)
    {
        // Ensure two different secrets
        var s1 = seed1.Get;
        var s2 = seed2.Get == s1 ? s1 + 1 : seed2.Get;

        var secret1 = MakeSecret(s1);
        var secret2 = MakeSecret(s2);
        var timestamp = "1700000000";
        var sig = Sign(secret1, msgId.Get, timestamp, payload.Get);
        var result = Verify(secret2, msgId.Get, timestamp, payload.Get, sig);
        return (!result).ToProperty();
    }

    // Property 4: signing is deterministic — same inputs always produce same output
    [Property(MaxTest = 100)]
    public Property Deterministic(PositiveInt seedVal, NonEmptyString msgId, NonEmptyString payload)
    {
        var secret = MakeSecret(seedVal.Get);
        var timestamp = "1700000000";
        var sig1 = Sign(secret, msgId.Get, timestamp, payload.Get);
        var sig2 = Sign(secret, msgId.Get, timestamp, payload.Get);
        return (sig1 == sig2).ToProperty();
    }
}
