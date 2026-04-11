using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Nahook.Tests;

/// <summary>
/// Webhook signature verification tests.
///
/// Validates that the Standard Webhooks signing format used by the Nahook API
/// can be correctly produced and verified using native crypto.
///
/// Signing spec:
///   base   = "{msgId}.{timestamp}.{payload}"
///   key    = base64_decode(secret_without_whsec_prefix)
///   sig    = "v1," + base64(HMAC-SHA256(key, base))
///   headers: webhook-id, webhook-timestamp, webhook-signature
/// </summary>
public class WebhookSignatureTests
{
    private const string TestSecret = "whsec_dGVzdF93ZWJob29rX3NpZ25pbmdfa2V5XzMyYnl0ZXMh";
    private const string MsgId = "msg_test_sig_001";
    private const string Timestamp = "1712345678";
    private const string Payload = "{\"order_id\":\"ord_123\",\"amount\":49.99}";

    private static string ComputeSignature(string secret, string msgId, string timestamp, string payload)
    {
        var rawSecret = secret.StartsWith("whsec_") ? secret[6..] : secret;
        var key = Convert.FromBase64String(rawSecret);

        var toSign = $"{msgId}.{timestamp}.{payload}";
        using var hmac = new HMACSHA256(key);
        var digest = hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign));

        return $"v1,{Convert.ToBase64String(digest)}";
    }

    [Fact]
    public void ProducesValidV1Signature()
    {
        var sig = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        Assert.Matches(@"^v1,[A-Za-z0-9+/]+=*$", sig);
    }

    [Fact]
    public void DeterministicSameInputsSameSignature()
    {
        var sig1 = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        var sig2 = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        Assert.Equal(sig1, sig2);
    }

    [Fact]
    public void RejectsTamperedPayload()
    {
        var original = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        var tampered = ComputeSignature(TestSecret, MsgId, Timestamp,
            "{\"order_id\":\"ord_123\",\"amount\":99.99}");
        Assert.NotEqual(original, tampered);
    }

    [Fact]
    public void RejectsWrongSecret()
    {
        var original = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        var wrong = ComputeSignature("whsec_d3Jvbmdfc2VjcmV0", MsgId, Timestamp, Payload);
        Assert.NotEqual(original, wrong);
    }

    [Fact]
    public void RejectsTamperedMsgId()
    {
        var original = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        var tampered = ComputeSignature(TestSecret, "msg_tampered_id", Timestamp, Payload);
        Assert.NotEqual(original, tampered);
    }

    [Fact]
    public void RejectsTamperedTimestamp()
    {
        var original = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        var tampered = ComputeSignature(TestSecret, MsgId, "9999999999", Payload);
        Assert.NotEqual(original, tampered);
    }

    [Fact]
    public void CorrectHeadersStructure()
    {
        var sig = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        var headers = new Dictionary<string, string>
        {
            ["content-type"] = "application/json",
            ["webhook-id"] = MsgId,
            ["webhook-timestamp"] = Timestamp,
            ["webhook-signature"] = sig,
        };

        Assert.StartsWith("msg_", headers["webhook-id"]);
        Assert.StartsWith("v1,", headers["webhook-signature"]);
        Assert.Matches(@"^\d+$", headers["webhook-timestamp"]);
        Assert.Equal("application/json", headers["content-type"]);
    }

    [Fact]
    public void HandlesSecretWithoutPrefix()
    {
        var rawSecret = TestSecret[6..];
        var withPrefix = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        var withoutPrefix = ComputeSignature(rawSecret, MsgId, Timestamp, Payload);
        Assert.Equal(withPrefix, withoutPrefix);
    }

    [Fact]
    public void MatchesKnownCrossLanguageReferenceSignature()
    {
        var sig = ComputeSignature(TestSecret, MsgId, Timestamp, Payload);
        Assert.Equal("v1,VF1JBS4kdSwmE64FeeiWTgszlPCfaop53x8bwzvHizw=", sig);
    }

    [Fact]
    public void EmptyPayloadProducesValidSignature()
    {
        var sig = ComputeSignature(TestSecret, MsgId, Timestamp, "");
        Assert.Equal("v1,yNFeVvBSs4aZ/sVHHw1MaUWnN1IGK/Ul/16T8aptSJo=", sig);
    }

    [Fact]
    public void UnicodePayloadConsistentAcrossLanguages()
    {
        var sig = ComputeSignature(TestSecret, MsgId, Timestamp,
            "{\"name\":\"caf\u00e9\",\"price\":\"\u20ac9.99\"}");
        Assert.Equal("v1,GcuGAMV9tELnF2rjay6sA8uo5PDPPlhaFi6gKUg06wQ=", sig);
    }
}
