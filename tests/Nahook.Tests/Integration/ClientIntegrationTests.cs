using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Nahook.Tests.Integration;

/// <summary>
/// Integration tests that hit a real Nahook API instance.
/// Skipped automatically when NAHOOK_TEST_API_URL is not set.
/// </summary>
public sealed class ClientIntegrationTests : IDisposable
{
    private readonly NahookClient? _client;
    private readonly NahookClient? _disabledClient;
    private readonly string? _endpointId;
    private readonly string? _eventType;
    private readonly bool _skip;

    public ClientIntegrationTests()
    {
        var apiUrl = Environment.GetEnvironmentVariable("NAHOOK_TEST_API_URL");
        var apiKey = Environment.GetEnvironmentVariable("NAHOOK_TEST_API_KEY");
        var disabledKey = Environment.GetEnvironmentVariable("NAHOOK_TEST_DISABLED_API_KEY");
        _endpointId = Environment.GetEnvironmentVariable("NAHOOK_TEST_ENDPOINT_ID");
        _eventType = Environment.GetEnvironmentVariable("NAHOOK_TEST_EVENT_TYPE");

        _skip = string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey);

        if (!_skip)
        {
            var opts = new NahookClientOptions { BaseUrl = apiUrl };
            _client = new NahookClient(apiKey!, opts);

            if (!string.IsNullOrEmpty(disabledKey))
                _disabledClient = new NahookClient(disabledKey, opts);
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
        _disabledClient?.Dispose();
    }

    // ──────────────────────────────────────────────
    // Send
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Send_HappyPath()
    {
        Skip.If(_skip, "Integration env vars not set");

        var result = await _client!.SendAsync(_endpointId!, new SendOptions
        {
            Payload = new Dictionary<string, object> { ["test"] = true, ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        });

        Assert.Equal("accepted", result.Status);
        Assert.StartsWith("del_", result.DeliveryId);
        Assert.False(string.IsNullOrEmpty(result.IdempotencyKey));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Send_IdempotencyDedup()
    {
        Skip.If(_skip, "Integration env vars not set");

        var idempotencyKey = $"idem-dedup-{Guid.NewGuid()}";
        var opts = new SendOptions
        {
            Payload = new Dictionary<string, object> { ["dedup"] = true },
            IdempotencyKey = idempotencyKey
        };

        var first = await _client!.SendAsync(_endpointId!, opts);
        var second = await _client!.SendAsync(_endpointId!, opts);

        Assert.Equal(first.DeliveryId, second.DeliveryId);
        Assert.Equal(idempotencyKey, first.IdempotencyKey);
        Assert.Equal(idempotencyKey, second.IdempotencyKey);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Send_SeparateKeys()
    {
        Skip.If(_skip, "Integration env vars not set");

        var result1 = await _client!.SendAsync(_endpointId!, new SendOptions
        {
            Payload = new Dictionary<string, object> { ["key"] = 1 },
            IdempotencyKey = $"sep-a-{Guid.NewGuid()}"
        });

        var result2 = await _client!.SendAsync(_endpointId!, new SendOptions
        {
            Payload = new Dictionary<string, object> { ["key"] = 2 },
            IdempotencyKey = $"sep-b-{Guid.NewGuid()}"
        });

        Assert.NotEqual(result1.DeliveryId, result2.DeliveryId);
    }

    // ──────────────────────────────────────────────
    // Trigger
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Trigger_FanOut()
    {
        Skip.If(_skip, "Integration env vars not set");

        var result = await _client!.TriggerAsync(_eventType!, new TriggerOptions
        {
            Payload = new Dictionary<string, object> { ["fan"] = "out", ["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        });

        Assert.Equal("accepted", result.Status);
        Assert.StartsWith("evt_", result.EventTypeId);
        Assert.True(result.DeliveryIds.Count >= 1, "Expected at least one delivery for a subscribed event type");
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Trigger_Unsubscribed()
    {
        Skip.If(_skip, "Integration env vars not set");

        var result = await _client!.TriggerAsync("nonexistent.unsubscribed.event", new TriggerOptions
        {
            Payload = new Dictionary<string, object> { ["ghost"] = true }
        });

        Assert.Empty(result.DeliveryIds);
    }

    // ──────────────────────────────────────────────
    // Batch
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task SendBatch_Accepted()
    {
        Skip.If(_skip, "Integration env vars not set");

        var result = await _client!.SendBatchAsync(new[]
        {
            new SendBatchItem
            {
                EndpointId = _endpointId!,
                Payload = new Dictionary<string, object> { ["batch"] = 1 },
                IdempotencyKey = $"batch-a-{Guid.NewGuid()}"
            },
            new SendBatchItem
            {
                EndpointId = _endpointId!,
                Payload = new Dictionary<string, object> { ["batch"] = 2 },
                IdempotencyKey = $"batch-b-{Guid.NewGuid()}"
            }
        });

        Assert.Equal(2, result.Items.Count);
        foreach (var item in result.Items)
        {
            Assert.Equal("accepted", item.Status);
            Assert.NotNull(item.DeliveryId);
            Assert.StartsWith("del_", item.DeliveryId!);
        }
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task TriggerBatch_Accepted()
    {
        Skip.If(_skip, "Integration env vars not set");

        var result = await _client!.TriggerBatchAsync(new[]
        {
            new TriggerBatchItem
            {
                EventType = _eventType!,
                Payload = new Dictionary<string, object> { ["tbatch"] = 1 }
            },
            new TriggerBatchItem
            {
                EventType = _eventType!,
                Payload = new Dictionary<string, object> { ["tbatch"] = 2 }
            }
        });

        Assert.Equal(2, result.Items.Count);
        foreach (var item in result.Items)
        {
            Assert.Equal("accepted", item.Status);
            Assert.NotNull(item.EventTypeId);
            Assert.StartsWith("evt_", item.EventTypeId!);
        }
    }

    // ──────────────────────────────────────────────
    // Error Cases
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Error_401_InvalidKey()
    {
        Skip.If(_skip, "Integration env vars not set");

        var apiUrl = Environment.GetEnvironmentVariable("NAHOOK_TEST_API_URL")!;
        using var badClient = new NahookClient("nhk_us_invalidkey_zzzz0000zzzz0000", new NahookClientOptions { BaseUrl = apiUrl });

        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            badClient.SendAsync(_endpointId!, new SendOptions
            {
                Payload = new Dictionary<string, object> { ["err"] = 401 }
            }));

        Assert.Equal(401, ex.Status);
        Assert.True(ex.IsAuthError);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Error_403_DisabledKey()
    {
        Skip.If(_skip, "Integration env vars not set");
        Skip.If(_disabledClient == null, "NAHOOK_TEST_DISABLED_API_KEY not set");

        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            _disabledClient!.SendAsync(_endpointId!, new SendOptions
            {
                Payload = new Dictionary<string, object> { ["err"] = 403 }
            }));

        Assert.Equal(403, ex.Status);
        Assert.Equal("token_disabled", ex.Code);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Error_404_MissingEndpoint()
    {
        Skip.If(_skip, "Integration env vars not set");

        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            _client!.SendAsync("ep_nonexistent_endpoint_xyz", new SendOptions
            {
                Payload = new Dictionary<string, object> { ["err"] = 404 }
            }));

        Assert.Equal(404, ex.Status);
        Assert.True(ex.IsNotFound);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    public async Task Error_400_InvalidEventType()
    {
        Skip.If(_skip, "Integration env vars not set");

        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            _client!.TriggerAsync("!!invalid event type!!", new TriggerOptions
            {
                Payload = new Dictionary<string, object> { ["err"] = 400 }
            }));

        Assert.Equal(400, ex.Status);
        Assert.True(ex.IsValidationError);
    }
}
