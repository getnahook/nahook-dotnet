using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nahook.Tests;

internal class TestHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.Accepted;
    public string ResponseBody { get; set; } = "{}";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        if (request.Content != null)
            LastRequestBody = await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return new HttpResponseMessage(ResponseStatusCode)
        {
            Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
        };
    }
}

// ──────────────────────────────────────────────
// NahookClient Tests
// ──────────────────────────────────────────────

public sealed class NahookClientConstructorTests
{
    [Fact]
    public void Rejects_empty_api_key()
    {
        Assert.Throws<ArgumentException>(() => new NahookClient(""));
    }

    [Fact]
    public void Rejects_api_key_without_nhk_prefix()
    {
        Assert.Throws<ArgumentException>(() => new NahookClient("sk_test_abc"));
    }

    [Fact]
    public void Accepts_valid_nhk_api_key()
    {
        using var handler = new TestHttpMessageHandler();
        using var client = new NahookClient("nhk_test_abc", handler);
        Assert.NotNull(client);
    }
}

public sealed class NahookClientSendTests
{
    [Fact]
    public async Task SendAsync_posts_to_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseBody = JsonSerializer.Serialize(new
            {
                deliveryId = "del_123",
                idempotencyKey = "key-1",
                status = "accepted"
            })
        };
        using var client = new NahookClient("nhk_test_abc", handler, new NahookClientOptions
        {
            BaseUrl = "https://test.nahook.com"
        });

        var result = await client.SendAsync("ep_abc", new SendOptions
        {
            Payload = new Dictionary<string, object> { ["event"] = "test" },
            IdempotencyKey = "key-1"
        });

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("https://test.nahook.com/api/ingest/ep_abc", handler.LastRequest.RequestUri!.ToString());
        Assert.Equal("del_123", result.DeliveryId);
        Assert.Equal("key-1", result.IdempotencyKey);

        // Verify body contains idempotencyKey
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("idempotencyKey", handler.LastRequestBody!);
        Assert.Contains("key-1", handler.LastRequestBody!);
    }

    [Fact]
    public async Task SendAsync_auto_generates_idempotency_key_when_missing()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseBody = JsonSerializer.Serialize(new
            {
                deliveryId = "del_123",
                idempotencyKey = "auto-gen",
                status = "accepted"
            })
        };
        using var client = new NahookClient("nhk_test_abc", handler);

        await client.SendAsync("ep_abc", new SendOptions
        {
            Payload = new Dictionary<string, object> { ["event"] = "test" }
        });

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("idempotencyKey", handler.LastRequestBody!);
        // Should contain a UUID-format key, not null
        Assert.DoesNotContain("\"idempotencyKey\":null", handler.LastRequestBody!);
    }
}

public sealed class NahookClientTriggerTests
{
    [Fact]
    public async Task TriggerAsync_posts_to_correct_url_without_idempotency_key()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseBody = JsonSerializer.Serialize(new
            {
                eventTypeId = "evt_123",
                deliveryIds = new[] { "del_1", "del_2" },
                status = "accepted"
            })
        };
        using var client = new NahookClient("nhk_test_abc", handler, new NahookClientOptions
        {
            BaseUrl = "https://test.nahook.com"
        });

        var result = await client.TriggerAsync("user.created", new TriggerOptions
        {
            Payload = new Dictionary<string, object> { ["userId"] = "u_1" }
        });

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://test.nahook.com/api/ingest/event/user.created", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal("evt_123", result.EventTypeId);
        Assert.Equal(2, result.DeliveryIds.Count);

        // Trigger should NOT have idempotencyKey
        Assert.DoesNotContain("idempotencyKey", handler.LastRequestBody!);
    }
}

public sealed class NahookClientBatchTests
{
    [Fact]
    public async Task SendBatchAsync_wraps_items_correctly()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Accepted,
            ResponseBody = JsonSerializer.Serialize(new { items = new[] { new { index = 0, deliveryId = "del_1", status = "accepted" } } })
        };
        using var client = new NahookClient("nhk_test_abc", handler);

        var result = await client.SendBatchAsync(new[]
        {
            new SendBatchItem { EndpointId = "ep_1", Payload = new Dictionary<string, object> { ["a"] = 1 } }
        });

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"items\"", handler.LastRequestBody!);
        Assert.Contains("ep_1", handler.LastRequestBody!);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task TriggerBatchAsync_wraps_items_correctly()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Accepted,
            ResponseBody = JsonSerializer.Serialize(new { items = new[] { new { index = 0, eventTypeId = "evt_1", deliveryIds = new[] { "del_1" }, status = "accepted" } } })
        };
        using var client = new NahookClient("nhk_test_abc", handler);

        var result = await client.TriggerBatchAsync(new[]
        {
            new TriggerBatchItem { EventType = "user.created", Payload = new Dictionary<string, object> { ["x"] = 1 } }
        });

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("\"items\"", handler.LastRequestBody!);
        Assert.Single(result.Items);
    }
}

// ──────────────────────────────────────────────
// NahookManagement Tests
// ──────────────────────────────────────────────

public sealed class NahookManagementConstructorTests
{
    [Fact]
    public void Rejects_empty_token()
    {
        Assert.Throws<ArgumentException>(() => new NahookManagement(""));
    }

    [Fact]
    public void Rejects_token_without_nhm_prefix()
    {
        Assert.Throws<ArgumentException>(() => new NahookManagement("nhk_test_abc"));
    }

    [Fact]
    public void Accepts_valid_nhm_token()
    {
        using var handler = new TestHttpMessageHandler();
        using var management = new NahookManagement("nhm_test_abc", handler);
        Assert.NotNull(management);
        Assert.NotNull(management.Endpoints);
        Assert.NotNull(management.EventTypes);
        Assert.NotNull(management.Applications);
        Assert.NotNull(management.Subscriptions);
        Assert.NotNull(management.PortalSessions);
    }

    [Fact]
    public void Management_options_has_no_retries_property()
    {
        var options = new NahookManagementOptions();
        // NahookManagementOptions should only have BaseUrl and TimeoutMs, no Retries.
        var props = typeof(NahookManagementOptions).GetProperties();
        Assert.DoesNotContain(props, p => p.Name == "Retries");
    }
}

// ──────────────────────────────────────────────
// Error Type Tests
// ──────────────────────────────────────────────

public sealed class ErrorTypeTests
{
    [Fact]
    public void NahookApiException_properties()
    {
        var ex = new NahookApiException("not found", 404, "not_found");
        Assert.Equal(404, ex.Status);
        Assert.Equal("not_found", ex.Code);
        Assert.True(ex.IsNotFound);
        Assert.False(ex.IsRetryable);
        Assert.False(ex.IsRateLimited);
        Assert.False(ex.IsAuthError);
    }

    [Fact]
    public void NahookApiException_retryable_on_500()
    {
        var ex = new NahookApiException("server error", 500, "internal_error");
        Assert.True(ex.IsRetryable);
    }

    [Fact]
    public void NahookApiException_retryable_on_429()
    {
        var ex = new NahookApiException("rate limited", 429, "rate_limit", retryAfter: 5);
        Assert.True(ex.IsRetryable);
        Assert.True(ex.IsRateLimited);
        Assert.Equal(5, ex.RetryAfter);
    }

    [Fact]
    public void NahookApiException_auth_error_on_401()
    {
        var ex = new NahookApiException("unauthorized", 401, "unauthorized");
        Assert.True(ex.IsAuthError);
    }

    [Fact]
    public void NahookApiException_auth_error_on_403_token_disabled()
    {
        var ex = new NahookApiException("forbidden", 403, "token_disabled");
        Assert.True(ex.IsAuthError);
    }

    [Fact]
    public void NahookApiException_not_auth_error_on_403_other()
    {
        var ex = new NahookApiException("forbidden", 403, "insufficient_permissions");
        Assert.False(ex.IsAuthError);
    }

    [Fact]
    public void NahookApiException_validation_error_on_400()
    {
        var ex = new NahookApiException("bad request", 400, "validation_error");
        Assert.True(ex.IsValidationError);
    }

    [Fact]
    public void NahookNetworkException_wraps_inner()
    {
        var inner = new HttpRequestException("Connection refused");
        var ex = new NahookNetworkException(inner);
        Assert.Equal("Network error: Connection refused", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void NahookTimeoutException_has_timeout_ms()
    {
        var ex = new NahookTimeoutException(5000);
        Assert.Equal(5000, ex.TimeoutMs);
        Assert.Equal("Request timed out after 5000ms", ex.Message);
    }
}

// ──────────────────────────────────────────────
// Management Resource Tests
// ──────────────────────────────────────────────

public sealed class ManagementResourceTests
{
    [Fact]
    public async Task Endpoints_ListAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] { new { id = "ep_1", url = "https://example.com", isActive = true, type = "http", createdAt = "2024-01-01", updatedAt = "2024-01-01" } })
        };
        using var mgmt = new NahookManagement("nhm_test_abc", handler, new NahookManagementOptions { BaseUrl = "https://test.nahook.com" });

        var result = await mgmt.Endpoints.ListAsync("ws_123");

        Assert.Equal("https://test.nahook.com/management/v1/workspaces/ws_123/endpoints", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task Endpoints_CreateAsync_sends_body()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { id = "ep_1", url = "https://example.com", isActive = true, type = "http", createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement("nhm_test_abc", handler);

        var result = await mgmt.Endpoints.CreateAsync("ws_123", new CreateEndpointOptions { Url = "https://example.com" });

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("https://example.com", handler.LastRequestBody!);
        Assert.Equal("ep_1", result.Id);
    }

    [Fact]
    public async Task Applications_ListAsync_passes_query_params()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(Array.Empty<object>())
        };
        using var mgmt = new NahookManagement("nhm_test_abc", handler, new NahookManagementOptions { BaseUrl = "https://test.nahook.com" });

        await mgmt.Applications.ListAsync("ws_123", new ListOptions { Limit = 10, Offset = 5 });

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("limit=10", uri);
        Assert.Contains("offset=5", uri);
    }

    [Fact]
    public async Task Subscriptions_DeleteAsync_uses_correct_path()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.NoContent,
            ResponseBody = ""
        };
        using var mgmt = new NahookManagement("nhm_test_abc", handler, new NahookManagementOptions { BaseUrl = "https://test.nahook.com" });

        await mgmt.Subscriptions.DeleteAsync("ws_123", "ep_abc", "evt_xyz");

        Assert.Equal(
            "https://test.nahook.com/management/v1/workspaces/ws_123/endpoints/ep_abc/subscriptions/evt_xyz",
            handler.LastRequest!.RequestUri!.ToString()
        );
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }

    [Fact]
    public async Task PortalSessions_CreateAsync_uses_correct_path()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { url = "https://portal.nahook.com/s/abc", code = "abc123", expiresAt = "2024-12-31" })
        };
        using var mgmt = new NahookManagement("nhm_test_abc", handler, new NahookManagementOptions { BaseUrl = "https://test.nahook.com" });

        var result = await mgmt.PortalSessions.CreateAsync("ws_123", "app_456");

        Assert.Equal(
            "https://test.nahook.com/management/v1/workspaces/ws_123/applications/app_456/portal",
            handler.LastRequest!.RequestUri!.ToString()
        );
        Assert.Equal("abc123", result.Code);
    }
}

// ──────────────────────────────────────────────
// HTTP Header Tests
// ──────────────────────────────────────────────

public sealed class HttpHeaderTests
{
    [Fact]
    public async Task Sends_authorization_header()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseBody = JsonSerializer.Serialize(new { deliveryId = "del_1", idempotencyKey = "k", status = "accepted" })
        };
        using var client = new NahookClient("nhk_test_secret", handler);

        await client.SendAsync("ep_1", new SendOptions { Payload = new Dictionary<string, object>() });

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("nhk_test_secret", handler.LastRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Sends_user_agent_header()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseBody = JsonSerializer.Serialize(new { deliveryId = "del_1", idempotencyKey = "k", status = "accepted" })
        };
        using var client = new NahookClient("nhk_test_secret", handler);

        await client.SendAsync("ep_1", new SendOptions { Payload = new Dictionary<string, object>() });

        Assert.Contains(handler.LastRequest!.Headers.UserAgent, p => p.Product != null && p.Product.Name == "nahook-dotnet");
    }
}

// ──────────────────────────────────────────────
// Regional Routing Tests
// ──────────────────────────────────────────────

public sealed class RegionalRoutingTests
{
    [Fact]
    public void ResolveBaseUrl_us_region_returns_us_base_url()
    {
        var url = NahookHttpClient.ResolveBaseUrl("nhk_us_abc123");
        Assert.Equal("https://us.api.nahook.com", url);
    }

    [Fact]
    public void ResolveBaseUrl_eu_region_returns_eu_base_url()
    {
        var url = NahookHttpClient.ResolveBaseUrl("nhk_eu_abc123");
        Assert.Equal("https://eu.api.nahook.com", url);
    }

    [Fact]
    public void ResolveBaseUrl_ap_region_returns_ap_base_url()
    {
        var url = NahookHttpClient.ResolveBaseUrl("nhk_ap_abc123");
        Assert.Equal("https://ap.api.nahook.com", url);
    }

    [Fact]
    public void ResolveBaseUrl_unknown_region_falls_back_to_default()
    {
        var url = NahookHttpClient.ResolveBaseUrl("nhk_zz_abc123");
        Assert.Equal("https://api.nahook.com", url);
    }

    [Fact]
    public void BaseUrl_option_overrides_region_resolution()
    {
        using var handler = new TestHttpMessageHandler();
        using var client = new NahookClient("nhk_eu_abc123", handler, new NahookClientOptions
        {
            BaseUrl = "https://custom.nahook.com"
        });

        // The client should use the explicit baseUrl, not the eu region URL.
        // We verify by making a request and checking the URL used.
        var result = client.SendAsync("ep_1", new SendOptions
        {
            Payload = new Dictionary<string, object> { ["e"] = "test" }
        }).GetAwaiter().GetResult();

        Assert.StartsWith("https://custom.nahook.com/", handler.LastRequest!.RequestUri!.ToString());
    }
}

// ──────────────────────────────────────────────
// Retry Delay Tests
// ──────────────────────────────────────────────

public sealed class RetryDelayTests
{
    [Fact]
    public void ComputeDelay_returns_value_between_zero_and_exponential_cap()
    {
        // attempt 0: cap = BaseDelayMs * 2^0 = 500
        // Full jitter: result should be in [0, 500]
        for (int i = 0; i < 100; i++)
        {
            int delay = NahookHttpClient.ComputeDelay(0, null);
            Assert.InRange(delay, 0, NahookHttpClient.BaseDelayMs);
        }
    }

    [Fact]
    public void ComputeDelay_caps_at_max_delay()
    {
        // attempt 20: exponential = 500 * 2^20 = huge, should be capped at MaxDelayMs
        for (int i = 0; i < 100; i++)
        {
            int delay = NahookHttpClient.ComputeDelay(20, null);
            Assert.InRange(delay, 0, NahookHttpClient.MaxDelayMs);
        }
    }

    [Fact]
    public void ComputeDelay_uses_retry_after_seconds_when_provided()
    {
        int delay = NahookHttpClient.ComputeDelay(0, 3);
        Assert.Equal(3000, delay);
    }
}
