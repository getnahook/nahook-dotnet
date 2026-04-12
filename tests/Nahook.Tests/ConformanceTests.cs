using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Nahook.Tests;

/// <summary>
/// Conformance tests driven by shared JSON fixtures in fixtures/conformance/.
/// Ensures cross-language consistency for error classification, region routing,
/// retry backoff, and signature verification.
/// </summary>
public sealed class ConformanceTests
{
    private static readonly string FixturesRoot;

    static ConformanceTests()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        // assemblyDir = tests/Nahook.Tests/bin/Debug/net8.0/
        // Go up 5 to nahook-dotnet/, then up 1 to public-sdks/, then into fixtures/conformance/
        FixturesRoot = Path.GetFullPath(Path.Combine(assemblyDir, "..", "..", "..", "..", "..", "..", "fixtures", "conformance"));
    }

    private static bool FixturesAvailable => Directory.Exists(FixturesRoot);

    private static JsonElement[] LoadCases(string category)
    {
        var path = Path.Combine(FixturesRoot, category, "cases.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<JsonElement[]>(json)!;
    }

    // ──────────────────────────────────────────────
    // Error Classification
    // ──────────────────────────────────────────────

    [SkippableFact]
    public void ErrorClassification_all_cases()
    {
        Skip.IfNot(FixturesAvailable, "Conformance fixtures not found at: " + FixturesRoot);
        var cases = LoadCases("error-classification");

        foreach (var c in cases)
        {
            var id = c.GetProperty("id").GetString()!;
            var input = c.GetProperty("input");
            var expect = c.GetProperty("expect");

            var status = input.GetProperty("status").GetInt32();
            var code = input.GetProperty("code").GetString()!;
            var message = input.GetProperty("message").GetString()!;
            int? retryAfter = input.TryGetProperty("retryAfter", out var ra) ? ra.GetInt32() : null;

            var ex = new NahookApiException(message, status, code, retryAfter);

            Assert.True(expect.GetProperty("isRetryable").GetBoolean() == ex.IsRetryable, $"{id}: isRetryable expected {expect.GetProperty("isRetryable")} got {ex.IsRetryable}");
            Assert.True(expect.GetProperty("isAuthError").GetBoolean() == ex.IsAuthError, $"{id}: isAuthError");
            Assert.True(expect.GetProperty("isNotFound").GetBoolean() == ex.IsNotFound, $"{id}: isNotFound");
            Assert.True(expect.GetProperty("isRateLimited").GetBoolean() == ex.IsRateLimited, $"{id}: isRateLimited");
            Assert.True(expect.GetProperty("isValidationError").GetBoolean() == ex.IsValidationError, $"{id}: isValidationError");
        }
    }

    // ──────────────────────────────────────────────
    // Region Routing
    // ──────────────────────────────────────────────

    [SkippableFact]
    public void RegionRouting_all_cases()
    {
        Skip.IfNot(FixturesAvailable, "Conformance fixtures not found at: " + FixturesRoot);
        var cases = LoadCases("region-routing");

        foreach (var c in cases)
        {
            var id = c.GetProperty("id").GetString()!;
            var token = c.GetProperty("input").GetProperty("token").GetString()!;
            var expectedUrl = c.GetProperty("expect").GetProperty("baseUrl").GetString()!;

            var actual = NahookHttpClient.ResolveBaseUrl(token);
            Assert.True(expectedUrl == actual, $"{id}: expected {expectedUrl} got {actual}");
        }
    }

    // ──────────────────────────────────────────────
    // Retry Backoff
    // ──────────────────────────────────────────────

    [SkippableFact]
    public void RetryBackoff_all_cases()
    {
        Skip.IfNot(FixturesAvailable, "Conformance fixtures not found at: " + FixturesRoot);
        var cases = LoadCases("retry-backoff");

        foreach (var c in cases)
        {
            var id = c.GetProperty("id").GetString()!;
            var input = c.GetProperty("input");
            var expect = c.GetProperty("expect");

            var attempt = input.GetProperty("attempt").GetInt32();
            int? retryAfterMs = input.TryGetProperty("retryAfterMs", out var ram) && ram.ValueKind != JsonValueKind.Null
                ? ram.GetInt32()
                : null;

            // Convert retryAfterMs to seconds for ComputeDelay (which takes seconds)
            int? retryAfterSeconds = retryAfterMs.HasValue ? retryAfterMs.Value / 1000 : null;

            if (expect.TryGetProperty("exactDelayMs", out var exact))
            {
                var delay = NahookHttpClient.ComputeDelay(attempt, retryAfterSeconds);
                Assert.True(exact.GetInt32() == delay, $"{id}: expected exactDelayMs {exact.GetInt32()} got {delay}");
            }
            else
            {
                var minDelay = expect.GetProperty("minDelayMs").GetInt32();
                var maxDelay = expect.GetProperty("maxDelayMs").GetInt32();

                for (int i = 0; i < 50; i++)
                {
                    var delay = NahookHttpClient.ComputeDelay(attempt, retryAfterSeconds);
                    Assert.True(delay >= minDelay && delay <= maxDelay, $"{id}: delay {delay} not in [{minDelay}, {maxDelay}]");
                }
            }
        }
    }

    // ──────────────────────────────────────────────
    // Signature
    // ──────────────────────────────────────────────

    private static string ComputeSignature(string secret, string msgId, string timestamp, string payload)
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

    private static bool VerifySignature(string secret, string msgId, string timestamp, string payload, string signature)
    {
        var expected = ComputeSignature(secret, msgId, timestamp, payload);
        return string.Equals(expected, signature, StringComparison.Ordinal);
    }

    private static string ResolvePayload(JsonElement input)
    {
        if (input.TryGetProperty("payloadGenerator", out var gen))
        {
            var genStr = gen.GetString()!;
            if (genStr.StartsWith("repeat_a_"))
            {
                var count = int.Parse(genStr.Replace("repeat_a_", ""));
                return new string('a', count);
            }
        }
        return input.GetProperty("payload").GetString()!;
    }

    [SkippableFact]
    public void Signature_all_cases()
    {
        Skip.IfNot(FixturesAvailable, "Conformance fixtures not found at: " + FixturesRoot);
        var cases = LoadCases("signature");

        foreach (var c in cases)
        {
            var id = c.GetProperty("id").GetString()!;
            var action = c.GetProperty("action").GetString()!;
            var input = c.GetProperty("input");
            var expect = c.GetProperty("expect");

            var secret = input.GetProperty("secret").GetString()!;
            var msgId = input.GetProperty("messageId").GetString()!;
            var timestamp = input.GetProperty("timestamp").GetString()!;
            var payload = ResolvePayload(input);

            switch (action)
            {
                case "sign_then_verify":
                {
                    var sig = ComputeSignature(secret, msgId, timestamp, payload);
                    var verifies = VerifySignature(secret, msgId, timestamp, payload, sig);
                    Assert.True(verifies == expect.GetProperty("verifies").GetBoolean(), $"{id}: sign_then_verify failed");
                    break;
                }

                case "sign_original_verify_tampered":
                {
                    var tampered = input.GetProperty("tamperedPayload").GetString()!;
                    var sig = ComputeSignature(secret, msgId, timestamp, payload);
                    var verifies = VerifySignature(secret, msgId, timestamp, tampered, sig);
                    Assert.True(verifies == expect.GetProperty("verifies").GetBoolean(), $"{id}: tampered payload should not verify");
                    break;
                }

                case "sign_with_original_verify_with_wrong":
                {
                    var wrongSecret = input.GetProperty("wrongSecret").GetString()!;
                    var sig = ComputeSignature(secret, msgId, timestamp, payload);
                    var verifies = VerifySignature(wrongSecret, msgId, timestamp, payload, sig);
                    Assert.True(verifies == expect.GetProperty("verifies").GetBoolean(), $"{id}: wrong secret should not verify");
                    break;
                }

                case "sign_twice_compare":
                {
                    var sig1 = ComputeSignature(secret, msgId, timestamp, payload);
                    var sig2 = ComputeSignature(secret, msgId, timestamp, payload);
                    Assert.True((sig1 == sig2) == expect.GetProperty("identical").GetBoolean(), $"{id}: determinism check failed");
                    break;
                }

                case "verify_known_signature":
                {
                    var expectedHeader = expect.GetProperty("signatureHeader").GetString()!;
                    var sig = ComputeSignature(secret, msgId, timestamp, payload);
                    // The expected format is "v1,{timestamp},{base64}" -- extract the base64 part
                    var parts = expectedHeader.Split(',');
                    if (parts.Length == 3)
                    {
                        var expectedSig = $"v1,{parts[2]}";
                        Assert.True(expectedSig == sig, $"{id}: expected {expectedSig} got {sig}");
                    }
                    else
                    {
                        Assert.True(expectedHeader == sig, $"{id}: expected {expectedHeader} got {sig}");
                    }
                    break;
                }

                default:
                    Assert.Fail($"Unknown action: {action} in case {id}");
                    break;
            }
        }
    }
}
