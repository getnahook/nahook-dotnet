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
        Assert.NotNull(management.Environments);
        Assert.NotNull(management.Deliveries);
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
// Management Endpoints Tests
// ──────────────────────────────────────────────

public sealed class NahookManagementEndpointsTests
{
    private const string Token = "nhm_test123";
    private const string BaseUrl = "https://test.nahook.com";

    [Fact]
    public async Task ListAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] { new { id = "ep_1", url = "https://example.com", isActive = true, type = "http", createdAt = "2024-01-01", updatedAt = "2024-01-01" } })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Endpoints.ListAsync("ws_123");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/endpoints", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(result.Data);
        Assert.Equal("ep_1", result.Data[0].Id);
    }

    [Fact]
    public async Task CreateAsync_sends_body_and_returns_endpoint()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { id = "ep_1", url = "https://example.com", isActive = true, type = "http", createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Endpoints.CreateAsync("ws_123", new CreateEndpointOptions { Url = "https://example.com" });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/endpoints", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("https://example.com", handler.LastRequestBody!);
        Assert.Equal("ep_1", result.Id);
        Assert.Equal("https://example.com", result.Url);
    }

    [Fact]
    public async Task GetAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "ep_1", url = "https://example.com", isActive = true, type = "http", createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Endpoints.GetAsync("ws_123", "ep_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/endpoints/ep_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("ep_1", result.Id);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task UpdateAsync_sends_patch_with_body()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "ep_1", url = "https://updated.com", isActive = false, type = "http", createdAt = "2024-01-01", updatedAt = "2024-01-02" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Endpoints.UpdateAsync("ws_123", "ep_1", new UpdateEndpointOptions { Url = "https://updated.com", IsActive = false });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/endpoints/ep_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Patch, handler.LastRequest.Method);
        Assert.Contains("https://updated.com", handler.LastRequestBody!);
        Assert.Equal("https://updated.com", result.Url);
    }

    [Fact]
    public async Task DeleteAsync_uses_correct_method_and_path()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.NoContent,
            ResponseBody = ""
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.Endpoints.DeleteAsync("ws_123", "ep_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/endpoints/ep_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }
}

// ──────────────────────────────────────────────
// Management Event Types Tests
// ──────────────────────────────────────────────

public sealed class NahookManagementEventTypesTests
{
    private const string Token = "nhm_test123";
    private const string BaseUrl = "https://test.nahook.com";

    [Fact]
    public async Task ListAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] { new { id = "evt_1", name = "order.created", description = "Order created", createdAt = "2024-01-01" } })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.EventTypes.ListAsync("ws_123");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/event-types", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(result.Data);
        Assert.Equal("evt_1", result.Data[0].Id);
        Assert.Equal("order.created", result.Data[0].Name);
    }

    [Fact]
    public async Task CreateAsync_sends_body_and_returns_event_type()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { id = "evt_2", name = "user.created", description = "User created", createdAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.EventTypes.CreateAsync("ws_123", new CreateEventTypeOptions { Name = "user.created", Description = "User created" });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/event-types", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("user.created", handler.LastRequestBody!);
        Assert.Contains("User created", handler.LastRequestBody!);
        Assert.Equal("evt_2", result.Id);
        Assert.Equal("user.created", result.Name);
    }

    [Fact]
    public async Task GetAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "evt_1", name = "order.created", description = "Order created", createdAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.EventTypes.GetAsync("ws_123", "evt_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/event-types/evt_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("evt_1", result.Id);
        Assert.Equal("order.created", result.Name);
    }

    [Fact]
    public async Task UpdateAsync_sends_patch_with_body()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "evt_1", name = "order.created", description = "Updated description", createdAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.EventTypes.UpdateAsync("ws_123", "evt_1", new UpdateEventTypeOptions { Description = "Updated description" });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/event-types/evt_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Patch, handler.LastRequest.Method);
        Assert.Contains("Updated description", handler.LastRequestBody!);
        Assert.Equal("Updated description", result.Description);
    }

    [Fact]
    public async Task DeleteAsync_uses_correct_method_and_path()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.NoContent,
            ResponseBody = ""
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.EventTypes.DeleteAsync("ws_123", "evt_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/event-types/evt_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }
}

// ──────────────────────────────────────────────
// Management Applications Tests
// ──────────────────────────────────────────────

public sealed class NahookManagementApplicationsTests
{
    private const string Token = "nhm_test123";
    private const string BaseUrl = "https://test.nahook.com";

    [Fact]
    public async Task ListAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] { new { id = "app_1", name = "My App", metadata = new Dictionary<string, string>(), createdAt = "2024-01-01", updatedAt = "2024-01-01" } })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.ListAsync("ws_123");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/applications", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(result.Data);
        Assert.Equal("app_1", result.Data[0].Id);
        Assert.Equal("My App", result.Data[0].Name);
    }

    [Fact]
    public async Task ListAsync_passes_pagination_query_params()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(Array.Empty<object>())
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.Applications.ListAsync("ws_123", new ListOptions { Limit = 10, Offset = 5 });

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("limit=10", uri);
        Assert.Contains("offset=5", uri);
    }

    [Fact]
    public async Task CreateAsync_sends_body_and_returns_application()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_2", name = "New App", externalId = "ext_1", metadata = new Dictionary<string, string>(), createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.CreateAsync("ws_123", new CreateApplicationOptions { Name = "New App", ExternalId = "ext_1" });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/applications", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("New App", handler.LastRequestBody!);
        Assert.Contains("ext_1", handler.LastRequestBody!);
        Assert.Equal("app_2", result.Id);
        Assert.Equal("New App", result.Name);
    }

    [Fact]
    public async Task GetAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_1", name = "My App", metadata = new Dictionary<string, string>(), createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.GetAsync("ws_123", "app_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/applications/app_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("app_1", result.Id);
    }

    [Fact]
    public async Task UpdateAsync_sends_patch_with_body()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_1", name = "Updated App", metadata = new Dictionary<string, string>(), createdAt = "2024-01-01", updatedAt = "2024-01-02" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.UpdateAsync("ws_123", "app_1", new UpdateApplicationOptions { Name = "Updated App" });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/applications/app_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Patch, handler.LastRequest.Method);
        Assert.Contains("Updated App", handler.LastRequestBody!);
        Assert.Equal("Updated App", result.Name);
    }

    [Fact]
    public async Task DeleteAsync_uses_correct_method_and_path()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.NoContent,
            ResponseBody = ""
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.Applications.DeleteAsync("ws_123", "app_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/applications/app_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }

    [Fact]
    public async Task ListEndpointsAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] { new { id = "ep_1", url = "https://example.com", isActive = true, type = "http", createdAt = "2024-01-01", updatedAt = "2024-01-01" } })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.ListEndpointsAsync("ws_123", "app_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/applications/app_1/endpoints", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(result.Data);
        Assert.Equal("ep_1", result.Data[0].Id);
    }

    [Fact]
    public async Task CreateEndpointAsync_sends_body_and_returns_endpoint()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { id = "ep_2", url = "https://new.example.com", isActive = true, type = "http", createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.CreateEndpointAsync("ws_123", "app_1", new CreateEndpointOptions { Url = "https://new.example.com" });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/applications/app_1/endpoints", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("https://new.example.com", handler.LastRequestBody!);
        Assert.Equal("ep_2", result.Id);
        Assert.Equal("https://new.example.com", result.Url);
    }

    // ── maxEndpoints + showEventTypes (tri-state PATCH semantics) ──

    [Fact]
    public async Task CreateAsync_with_MaxEndpoints_includes_it_in_body()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_2", name = "Capped", metadata = new Dictionary<string, string>(), maxEndpoints = 2, showEventTypes = true, createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.CreateAsync("ws_123", new CreateApplicationOptions { Name = "Capped", MaxEndpoints = 2 });

        var body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        Assert.Equal(2, body.GetProperty("maxEndpoints").GetInt32());
        Assert.Equal(2, result.MaxEndpoints);
        Assert.True(result.ShowEventTypes);
    }

    [Fact]
    public async Task CreateAsync_with_ShowEventTypes_false_includes_it_in_body()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_2", name = "Hidden", metadata = new Dictionary<string, string>(), maxEndpoints = (int?)null, showEventTypes = false, createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.CreateAsync("ws_123", new CreateApplicationOptions { Name = "Hidden", ShowEventTypes = false });

        var body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        Assert.False(body.GetProperty("showEventTypes").GetBoolean());
        Assert.Null(result.MaxEndpoints);
        Assert.False(result.ShowEventTypes);
    }

    [Fact]
    public async Task CreateAsync_omits_unset_cap_fields()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_2", name = "Plain", metadata = new Dictionary<string, string>(), createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.Applications.CreateAsync("ws_123", new CreateApplicationOptions { Name = "Plain" });

        var body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        Assert.False(body.TryGetProperty("maxEndpoints", out _));
        Assert.False(body.TryGetProperty("showEventTypes", out _));
    }

    [Fact]
    public async Task UpdateAsync_OfNull_sends_explicit_null_to_clear_the_cap()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_1", name = "My App", metadata = new Dictionary<string, string>(), maxEndpoints = (int?)null, showEventTypes = true, createdAt = "2024-01-01", updatedAt = "2024-01-02" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.UpdateAsync("ws_123", "app_1", new UpdateApplicationOptions { MaxEndpoints = NullableInt.OfNull() });

        var body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        Assert.True(body.TryGetProperty("maxEndpoints", out var prop), "maxEndpoints must be present as explicit null");
        Assert.Equal(JsonValueKind.Null, prop.ValueKind);
        Assert.Null(result.MaxEndpoints);
    }

    [Fact]
    public async Task UpdateAsync_omits_cap_fields_when_unset()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_1", name = "Renamed", metadata = new Dictionary<string, string>(), createdAt = "2024-01-01", updatedAt = "2024-01-02" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.Applications.UpdateAsync("ws_123", "app_1", new UpdateApplicationOptions { Name = "Renamed" });

        var body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        Assert.False(body.TryGetProperty("maxEndpoints", out _));
        Assert.False(body.TryGetProperty("showEventTypes", out _));
    }

    [Fact]
    public async Task UpdateAsync_Of_sends_value_and_response_exposes_fields()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_1", name = "My App", metadata = new Dictionary<string, string>(), maxEndpoints = 5, showEventTypes = false, createdAt = "2024-01-01", updatedAt = "2024-01-02" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.UpdateAsync("ws_123", "app_1", new UpdateApplicationOptions { MaxEndpoints = NullableInt.Of(5), ShowEventTypes = false });

        var body = JsonDocument.Parse(handler.LastRequestBody!).RootElement;
        Assert.Equal(5, body.GetProperty("maxEndpoints").GetInt32());
        Assert.False(body.GetProperty("showEventTypes").GetBoolean());
        Assert.Equal(5, result.MaxEndpoints);
        Assert.False(result.ShowEventTypes);
    }

    [Fact]
    public async Task GetAsync_response_without_cap_fields_defaults_ShowEventTypes_true()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "app_1", name = "My App", metadata = new Dictionary<string, string>(), createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Applications.GetAsync("ws_123", "app_1");

        Assert.Null(result.MaxEndpoints);
        Assert.True(result.ShowEventTypes);
    }
}

// ──────────────────────────────────────────────
// Management Subscriptions Tests
// ──────────────────────────────────────────────

public sealed class NahookManagementSubscriptionsTests
{
    private const string Token = "nhm_test123";
    private const string BaseUrl = "https://test.nahook.com";

    [Fact]
    public async Task ListAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] { new { id = "sub_1", eventTypeId = "evt_1", eventTypeName = "order.created", createdAt = "2024-01-01" } })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Subscriptions.ListAsync("ws_123", "ep_abc");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/endpoints/ep_abc/subscriptions", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(result.Data);
        Assert.Equal("sub_1", result.Data[0].Id);
        Assert.Equal("evt_1", result.Data[0].EventTypeId);
    }

    [Fact]
    public async Task CreateAsync_sends_body_and_returns_result()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { subscribed = 2 })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Subscriptions.CreateAsync("ws_123", "ep_abc", new CreateSubscriptionOptions
        {
            EventTypeIds = new List<string> { "evt_1", "evt_2" }
        });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/endpoints/ep_abc/subscriptions", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("evt_1", handler.LastRequestBody!);
        Assert.Contains("evt_2", handler.LastRequestBody!);
        Assert.Equal(2, result.Subscribed);
    }

    [Fact]
    public async Task DeleteAsync_uses_correct_method_and_path()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.NoContent,
            ResponseBody = ""
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.Subscriptions.DeleteAsync("ws_123", "ep_abc", "evt_xyz");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/endpoints/ep_abc/subscriptions/evt_xyz", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }
}

// ──────────────────────────────────────────────
// Management Portal Sessions Tests
// ──────────────────────────────────────────────

public sealed class NahookManagementPortalSessionsTests
{
    private const string Token = "nhm_test123";
    private const string BaseUrl = "https://test.nahook.com";

    [Fact]
    public async Task CreateAsync_calls_correct_url_and_returns_session()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { url = "https://portal.nahook.com/s/abc", code = "abc123", expiresAt = "2024-12-31" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.PortalSessions.CreateAsync("ws_123", "app_456");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/applications/app_456/portal", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Equal("https://portal.nahook.com/s/abc", result.Url);
        Assert.Equal("abc123", result.Code);
        Assert.Equal("2024-12-31", result.ExpiresAt);
    }

    [Fact]
    public async Task CreateAsync_with_metadata_sends_body()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { url = "https://portal.nahook.com/s/def", code = "def456", expiresAt = "2024-12-31" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.PortalSessions.CreateAsync("ws_123", "app_456", new CreatePortalSessionOptions
        {
            Metadata = new Dictionary<string, string> { ["tenant"] = "acme" }
        });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/applications/app_456/portal", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("acme", handler.LastRequestBody!);
        Assert.Equal("def456", result.Code);
    }
}

// ──────────────────────────────────────────────
// Management Environments Tests
// ──────────────────────────────────────────────

public sealed class NahookManagementEnvironmentsTests
{
    private const string Token = "nhm_test123";
    private const string BaseUrl = "https://test.nahook.com";

    [Fact]
    public async Task ListAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] { new { id = "env_1", name = "Production", slug = "production", isDefault = true, createdAt = "2024-01-01", updatedAt = "2024-01-01" } })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Environments.ListAsync("ws_123");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/environments", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(result.Data);
        Assert.Equal("env_1", result.Data[0].Id);
        Assert.Equal("Production", result.Data[0].Name);
    }

    [Fact]
    public async Task CreateAsync_sends_body_and_returns_environment()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.Created,
            ResponseBody = JsonSerializer.Serialize(new { id = "env_2", name = "Staging", slug = "staging", isDefault = false, createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Environments.CreateAsync("ws_123", new CreateEnvironmentOptions { Name = "Staging", Slug = "staging" });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/environments", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Post, handler.LastRequest.Method);
        Assert.Contains("Staging", handler.LastRequestBody!);
        Assert.Contains("staging", handler.LastRequestBody!);
        Assert.Equal("env_2", result.Id);
        Assert.Equal("Staging", result.Name);
        Assert.Equal("staging", result.Slug);
    }

    [Fact]
    public async Task GetAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "env_1", name = "Production", slug = "production", isDefault = true, createdAt = "2024-01-01", updatedAt = "2024-01-01" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Environments.GetAsync("ws_123", "env_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/environments/env_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("env_1", result.Id);
        Assert.True(result.IsDefault);
    }

    [Fact]
    public async Task UpdateAsync_sends_patch_with_body()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { id = "env_1", name = "Prod", slug = "production", isDefault = true, createdAt = "2024-01-01", updatedAt = "2024-01-02" })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Environments.UpdateAsync("ws_123", "env_1", new UpdateEnvironmentOptions { Name = "Prod" });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/environments/env_1", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Patch, handler.LastRequest.Method);
        Assert.Contains("Prod", handler.LastRequestBody!);
        Assert.Equal("Prod", result.Name);
    }

    [Fact]
    public async Task DeleteAsync_uses_correct_method_and_path()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.NoContent,
            ResponseBody = ""
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.Environments.DeleteAsync("ws_123", "env_2");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/environments/env_2", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Delete, handler.LastRequest.Method);
    }

    [Fact]
    public async Task ListEventTypeVisibilityAsync_calls_correct_url()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] { new { eventTypeId = "evt_1", eventTypeName = "order.created", published = true } })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Environments.ListEventTypeVisibilityAsync("ws_123", "env_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/environments/env_1/event-types", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Single(result.Data);
        Assert.Equal("evt_1", result.Data[0].EventTypeId);
        Assert.True(result.Data[0].Published);
    }

    [Fact]
    public async Task SetEventTypeVisibilityAsync_sends_put_with_body()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new { eventTypeId = "evt_1", eventTypeName = "order.created", published = false })
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Environments.SetEventTypeVisibilityAsync("ws_123", "env_1", "evt_1", new SetVisibilityOptions { Published = false });

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_123/environments/env_1/event-types/evt_1/visibility", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Put, handler.LastRequest.Method);
        Assert.Contains("published", handler.LastRequestBody!);
        Assert.Equal("evt_1", result.EventTypeId);
        Assert.False(result.Published);
    }
}

// ──────────────────────────────────────────────
// HTTP Header Tests
// ──────────────────────────────────────────────

public sealed class HttpHeaderTests
{
    [Fact]
    public async Task Sends_authorization_bearer_header()
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
    public async Task Sends_user_agent_starting_with_nahook_dotnet()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseBody = JsonSerializer.Serialize(new { deliveryId = "del_1", idempotencyKey = "k", status = "accepted" })
        };
        using var client = new NahookClient("nhk_test_secret", handler);

        await client.SendAsync("ep_1", new SendOptions { Payload = new Dictionary<string, object>() });

        Assert.Contains(handler.LastRequest!.Headers.UserAgent, p => p.Product != null && p.Product.Name == "nahook-dotnet");
    }

    [Fact]
    public async Task Post_request_includes_content_type_json()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseBody = JsonSerializer.Serialize(new { deliveryId = "del_1", idempotencyKey = "k", status = "accepted" })
        };
        using var client = new NahookClient("nhk_test_secret", handler);

        await client.SendAsync("ep_1", new SendOptions { Payload = new Dictionary<string, object>() });

        Assert.NotNull(handler.LastRequest!.Content);
        Assert.Equal("application/json", handler.LastRequest.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Get_request_has_no_content_type()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] { new { id = "ep_1", url = "https://example.com", isActive = true, type = "http", createdAt = "2024-01-01", updatedAt = "2024-01-01" } })
        };
        using var mgmt = new NahookManagement("nhm_test_secret", handler);

        await mgmt.Endpoints.ListAsync("ws_123");

        Assert.Null(handler.LastRequest!.Content);
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
    public async Task BaseUrl_option_overrides_region_resolution()
    {
        using var handler = new TestHttpMessageHandler();
        using var client = new NahookClient("nhk_eu_abc123", handler, new NahookClientOptions
        {
            BaseUrl = "https://custom.nahook.com"
        });

        // The client should use the explicit baseUrl, not the eu region URL.
        // We verify by making a request and checking the URL used.
        await client.SendAsync("ep_1", new SendOptions
        {
            Payload = new Dictionary<string, object> { ["e"] = "test" }
        });

        Assert.StartsWith("https://custom.nahook.com/", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task BaseUrl_option_overrides_region_resolution_for_trigger()
    {
        // Parallel to the send() test above — region-prefixed key must NOT
        // win over an explicit BaseUrl when calling TriggerAsync either.
        // Catches a regression where the override is wired through SendAsync
        // but skipped for TriggerAsync (different code path in the client).
        using var handler = new TestHttpMessageHandler
        {
            ResponseBody = JsonSerializer.Serialize(new
            {
                eventTypeId = "evt_x",
                deliveryIds = Array.Empty<string>(),
                status = "accepted"
            })
        };
        using var client = new NahookClient("nhk_eu_abc123", handler, new NahookClientOptions
        {
            BaseUrl = "https://custom.nahook.com"
        });

        await client.TriggerAsync("user.created", new TriggerOptions
        {
            Payload = new Dictionary<string, object> { ["e"] = "test" }
        });

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

// ──────────────────────────────────────────────
// Management Deliveries Tests
// ──────────────────────────────────────────────

public sealed class NahookManagementDeliveriesTests
{
    private const string Token = "nhm_test123";
    private const string BaseUrl = "https://test.nahook.com";

    private static string DeliveryJson(string id, string status, bool hasPayload, int totalAttempts) =>
        JsonSerializer.Serialize(new
        {
            id,
            idempotencyKey = "k-" + id,
            endpointId = "ep_1",
            status,
            totalAttempts,
            firstAttemptAt = "2026-05-28T14:30:59Z",
            deliveredAt = status == "delivered" ? "2026-05-28T14:30:59Z" : null,
            nextRetryAt = (string?)null,
            hasPayload,
            createdAt = "2026-05-28T14:30:59Z",
            updatedAt = "2026-05-28T14:30:59Z"
        });

    [Fact]
    public async Task ListAsync_returns_paginated_data_and_next_cursor()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = "{\"deliveries\":[" + DeliveryJson("del_a", "delivered", true, 1) + "," + DeliveryJson("del_b", "failed", false, 3) + "],\"nextCursor\":\"opaque-token-aaa\"}"
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Deliveries.ListAsync("ws_abc", "ep_1");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_abc/endpoints/ep_1/deliveries", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("del_a", result.Data[0].Id);
        Assert.Equal("opaque-token-aaa", result.NextCursor);
    }

    [Fact]
    public async Task ListAsync_returns_null_cursor_when_last_page()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = "{\"deliveries\":[],\"nextCursor\":null}"
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var result = await mgmt.Deliveries.ListAsync("ws_abc", "ep_1");

        Assert.Empty(result.Data);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task ListAsync_forwards_query_params()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = "{\"deliveries\":[],\"nextCursor\":null}"
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.Deliveries.ListAsync("ws_abc", "ep_1", new ListDeliveriesOptions
        {
            Limit = 25,
            Cursor = "opaque-token-xyz",
            Status = "failed"
        });

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("limit=25", uri);
        Assert.Contains("cursor=opaque-token-xyz", uri);
        Assert.Contains("status=failed", uri);
    }

    [Fact]
    public async Task ListAsync_omits_unset_query_params()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = "{\"deliveries\":[],\"nextCursor\":null}"
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        await mgmt.Deliveries.ListAsync("ws_abc", "ep_1");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.DoesNotContain("?", uri);
        Assert.DoesNotContain("limit", uri);
        Assert.DoesNotContain("cursor", uri);
        Assert.DoesNotContain("status", uri);
    }

    [Fact]
    public async Task GetAsync_returns_metadata_without_envelope_by_default()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = DeliveryJson("del_a", "delivered", true, 1)
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var delivery = await mgmt.Deliveries.GetAsync("ws_abc", "del_a");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_abc/deliveries/del_a", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal("del_a", delivery.Id);
        Assert.True(delivery.HasPayload);
        Assert.Null(delivery.Payload);
    }

    [Fact]
    public async Task GetAsync_with_include_payload_returns_envelope()
    {
        string body = "{\"id\":\"del_a\",\"idempotencyKey\":\"k1\",\"endpointId\":\"ep_1\",\"status\":\"delivered\",\"totalAttempts\":1,\"firstAttemptAt\":\"2026-05-28T14:30:59Z\",\"deliveredAt\":\"2026-05-28T14:30:59Z\",\"nextRetryAt\":null,\"hasPayload\":true,\"createdAt\":\"2026-05-28T14:30:59Z\",\"updatedAt\":\"2026-05-28T14:30:59Z\",\"payload\":{\"status\":\"available\",\"data\":{\"orderId\":\"ord_123\"},\"contentType\":\"application/json\"}}";
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = body
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var delivery = await mgmt.Deliveries.GetAsync("ws_abc", "del_a", new GetDeliveryOptions { IncludePayload = true });

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("include=payload", uri);
        Assert.NotNull(delivery.Payload);
        Assert.Equal("available", delivery.Payload!.Status);
        Assert.Equal("application/json", delivery.Payload!.ContentType);
        Assert.NotNull(delivery.Payload!.Data);
        Assert.Equal("ord_123", delivery.Payload!.Data!.Value.GetProperty("orderId").GetString());
    }

    [Fact]
    public async Task GetAsync_returns_forbidden_envelope_for_plan_gated_workspace()
    {
        string body = "{\"id\":\"del_a\",\"idempotencyKey\":\"k1\",\"endpointId\":\"ep_1\",\"status\":\"delivered\",\"totalAttempts\":1,\"firstAttemptAt\":null,\"deliveredAt\":\"2026-05-28T14:30:59Z\",\"nextRetryAt\":null,\"hasPayload\":true,\"createdAt\":\"2026-05-28T14:30:59Z\",\"updatedAt\":\"2026-05-28T14:30:59Z\",\"payload\":{\"status\":\"forbidden\"}}";
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = body
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var delivery = await mgmt.Deliveries.GetAsync("ws_abc", "del_a", new GetDeliveryOptions { IncludePayload = true });

        Assert.NotNull(delivery.Payload);
        Assert.Equal("forbidden", delivery.Payload!.Status);
        Assert.Null(delivery.Payload!.Data);
        Assert.Null(delivery.Payload!.ContentType);
    }

    [Fact]
    public async Task GetAttemptsAsync_returns_array()
    {
        string body = "[{\"id\":\"att_1\",\"attemptNumber\":1,\"status\":\"failed\",\"responseStatusCode\":502,\"responseTimeMs\":142,\"errorMessage\":\"Bad gateway\",\"createdAt\":\"2026-05-28T14:31:00Z\"},{\"id\":\"att_2\",\"attemptNumber\":2,\"status\":\"success\",\"responseStatusCode\":200,\"responseTimeMs\":88,\"errorMessage\":null,\"createdAt\":\"2026-05-28T14:31:30Z\"}]";
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = body
        };
        using var mgmt = new NahookManagement(Token, handler, new NahookManagementOptions { BaseUrl = BaseUrl });

        var attempts = await mgmt.Deliveries.GetAttemptsAsync("ws_abc", "del_a");

        Assert.Equal($"{BaseUrl}/management/v1/workspaces/ws_abc/deliveries/del_a/attempts", handler.LastRequest!.RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
        Assert.Equal(2, attempts.Count);
        Assert.Equal(1, attempts[0].AttemptNumber);
        Assert.Equal("success", attempts[1].Status);
        Assert.Equal(502, attempts[0].ResponseStatusCode);
    }
}
