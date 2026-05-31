# Nahook .NET SDK

Official .NET SDK for the [Nahook](https://nahook.com) webhook platform.

Two classes, one package:

| Class | Purpose | Auth |
|-------|---------|------|
| [`NahookClient`](#nahookclient) | Send and trigger webhook events | API key (`nhk_us_...`) |
| [`NahookManagement`](#nahookmanagement) | Manage endpoints, event types, apps | Management token (`nhm_...`) |

## Requirements

- .NET 6+

## Installation

```bash
dotnet add package Nahook
```

Or via the NuGet Package Manager:

```
Install-Package Nahook
```

---

## NahookClient

Send webhooks to specific endpoints or fan-out by event type. Implements `IDisposable`.

### Setup

```csharp
using Nahook;

var client = new NahookClient("nhk_us_...");

// With options:
var client = new NahookClient("nhk_us_...", new NahookClientOptions
{
    TimeoutMs = 10_000,                  // default: 30_000ms
    Retries = 3,                         // default: 0 (no retries)
});
```

### Configuration

The SDK automatically routes requests to the correct regional API based on your API key prefix (`nhk_us_...` -> US, `nhk_eu_...` -> EU, `nhk_ap_...` -> Asia Pacific). No configuration needed.

To override the base URL (for testing or local development):

```csharp
var client = new NahookClient("nhk_us_...", new NahookClientOptions
{
    BaseUrl = "http://localhost:3001",
});
```

For unit tests, mock the SDK client at the dependency injection boundary. For integration tests, override the base URL to point at a local server.

### Advanced HTTP configuration

The SDK ships with a `SocketsHttpHandler` configured for keep-alive plus a 5-minute
`PooledConnectionLifetime` — the pool cycles connections (and re-resolves DNS) instead
of pinning to the IP that was resolved at process start. Without this, a long-running
process can keep talking to a stale IP after a deploy / failover / DNS change. The
defaults are:

| Setting | Value |
|---|---|
| `PooledConnectionLifetime` | 5 minutes |
| `PooledConnectionIdleTimeout` | 2 minutes |
| `MaxConnectionsPerServer` | 50 |
| `AutomaticDecompression` | All |

For most apps the defaults are enough. Two escape hatches when you want more control:

**Plug in an `IHttpClientFactory` client.** This is the recommended pattern in ASP.NET
DI — let the framework manage `HttpClient` lifetime and any handler pipeline (Polly,
auth refresh, telemetry, etc.):

```csharp
// Program.cs
builder.Services.AddHttpClient("nahook")
    .AddPolicyHandler(Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .OrResult(r => (int)r.StatusCode >= 500)
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

// usage
var httpClient = httpClientFactory.CreateClient("nahook");
var client = new NahookClient("nhk_us_...", new NahookClientOptions
{
    HttpClient = httpClient,
});
```

When `HttpClient` is supplied, the SDK uses it verbatim. The caller-set
`HttpClient.Timeout` governs request timeouts (and is what `NahookTimeoutException.TimeoutMs`
reports). The SDK will NOT dispose the supplied `HttpClient` on `NahookClient.Dispose()` —
the caller owns its lifecycle.

**Supply only a handler.** When you want the SDK to manage the `HttpClient` but still
swap the underlying handler:

```csharp
var handler = new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(1),
    Proxy = new WebProxy("http://corp-proxy:8080"),
    SslOptions = new SslClientAuthenticationOptions { /* mTLS, etc. */ },
};

var client = new NahookClient("nhk_us_...", new NahookClientOptions
{
    Handler = handler,
});
```

The SDK wraps the handler in its own `HttpClient` and disposes that wrapper on
`Dispose()`, but does NOT dispose the handler — the caller owns it. Ignored when
`HttpClient` is also supplied. The same `HttpClient` and `Handler` options are accepted
by `NahookManagementOptions`.

### Send to a specific endpoint

```csharp
var result = await client.SendAsync("ep_abc123", new
{
    OrderId = "123",
    Status = "paid",
});
// result.DeliveryId  -> "del_..."
// result.Status      -> "accepted"
```

### Fan-out by event type

```csharp
var result = await client.TriggerAsync("order.paid", new
{
    OrderId = "123",
    Status = "paid",
});
// result.EventTypeId  -> "evt_..."
// result.DeliveryIds  -> ["del_..."]
// result.Status       -> "accepted"
```

### Batch operations

```csharp
// Send to multiple endpoints (max 20 items)
var batch = await client.SendBatchAsync(new[]
{
    new SendBatchItem { EndpointId = "ep_abc", Payload = new { OrderId = "123" } },
    new SendBatchItem { EndpointId = "ep_def", Payload = new { OrderId = "456" } },
});

// Fan-out multiple event types (max 20 items)
var fanOut = await client.TriggerBatchAsync(new[]
{
    new TriggerBatchItem { EventType = "order.paid", Payload = new { OrderId = "123" } },
    new TriggerBatchItem { EventType = "order.shipped", Payload = new { OrderId = "456" } },
});

// Results: 202 (all succeed) or 207 (mixed)
foreach (var item in batch.Items)
{
    if (item.Error != null)
    {
        Console.WriteLine($"Item {item.Index} failed: {item.Error.Code}");
    }
}
```

### Retry behavior

Retries are opt-in via the `Retries` constructor option. When enabled:

- **Strategy:** Exponential backoff with full jitter
- **Delays:** 500ms base, 10s max
- **Retryable:** 5xx, 429 (respects `Retry-After`), network errors, timeouts
- **Non-retryable:** 400, 401, 403, 404, 409, 413
- **Safe by design:** Idempotency keys are always sent, making retries safe

### Dispose

`NahookClient` implements `IDisposable`. Use `using` statements or call `Dispose()` when done:

```csharp
using var client = new NahookClient("nhk_us_...");
await client.SendAsync("ep_abc", new { OrderId = "123" });
// HttpClient is disposed automatically
```

---

## NahookManagement

Programmatically manage your Nahook workspace resources. Implements `IDisposable`.

### Setup

```csharp
using Nahook;

// Simple
var mgmt = new NahookManagement("nhm_...");

// With options
var mgmt = new NahookManagement("nhm_...", new NahookManagementOptions
{
    TimeoutMs = 30_000,                  // default
});
```

### Endpoints

```csharp
var endpoints = await mgmt.Endpoints.ListAsync("ws_abc");

var endpoint = await mgmt.Endpoints.CreateAsync("ws_abc", new CreateEndpointOptions
{
    Url = "https://example.com/webhooks",
    Description = "Production webhook",
    Type = "webhook", // "webhook" | "slack"
});

var endpoint = await mgmt.Endpoints.GetAsync("ws_abc", "ep_123");

await mgmt.Endpoints.UpdateAsync("ws_abc", "ep_123", new UpdateEndpointOptions
{
    Description = "Updated",
    IsActive = false,
});

await mgmt.Endpoints.DeleteAsync("ws_abc", "ep_123");
```

### Event Types

```csharp
var eventTypes = await mgmt.EventTypes.ListAsync("ws_abc");

var eventType = await mgmt.EventTypes.CreateAsync("ws_abc", new CreateEventTypeOptions
{
    Name = "order.paid",
    Description = "Fired when an order is paid",
});

var eventType = await mgmt.EventTypes.GetAsync("ws_abc", "evt_123");

await mgmt.EventTypes.UpdateAsync("ws_abc", "evt_123", new UpdateEventTypeOptions
{
    Description = "Updated description",
});

await mgmt.EventTypes.DeleteAsync("ws_abc", "evt_123");
```

### Applications

```csharp
var apps = await mgmt.Applications.ListAsync("ws_abc");

var app = await mgmt.Applications.CreateAsync("ws_abc", new CreateApplicationOptions
{
    Name = "Acme Corp",
    ExternalId = "acme-123",
});

var app = await mgmt.Applications.GetAsync("ws_abc", "app_123");

await mgmt.Applications.UpdateAsync("ws_abc", "app_123", new UpdateApplicationOptions
{
    Name = "Acme Inc",
});

await mgmt.Applications.DeleteAsync("ws_abc", "app_123");

// Endpoints scoped to an application
var endpoints = await mgmt.Applications.ListEndpointsAsync("ws_abc", "app_123");

var ep = await mgmt.Applications.CreateEndpointAsync("ws_abc", "app_123", new CreateEndpointOptions
{
    Url = "https://acme.com/webhooks",
});
```

### Subscriptions

```csharp
var subs = await mgmt.Subscriptions.ListAsync("ws_abc", "ep_123");

await mgmt.Subscriptions.CreateAsync("ws_abc", "ep_123", new CreateSubscriptionOptions
{
    EventTypeIds = new[] { "evt_456" },
});

await mgmt.Subscriptions.DeleteAsync("ws_abc", "ep_123", "evt_456");
```

### Environments

```csharp
var envs = await mgmt.Environments.ListAsync("ws_abc");

var env = await mgmt.Environments.CreateAsync("ws_abc", new CreateEnvironmentOptions
{
    Name = "Staging",
    Slug = "staging",
});

var env = await mgmt.Environments.GetAsync("ws_abc", "env_123");

await mgmt.Environments.UpdateAsync("ws_abc", "env_123", new UpdateEnvironmentOptions
{
    Name = "Pre-production",
});

await mgmt.Environments.DeleteAsync("ws_abc", "env_123");
```

> **Note:** The `Nahook.Environment` model class can shadow `System.Environment`. If you need both in the same file, use a fully qualified name or an alias:
> ```csharp
> using NahookEnv = Nahook.Environment;
> ```

### Event Type Visibility

Control which event types are visible per environment.

```csharp
var visibility = await mgmt.Environments.ListEventTypeVisibilityAsync("ws_abc", "env_123");

var vis = await mgmt.Environments.SetEventTypeVisibilityAsync("ws_abc", "env_123", "evt_456", new SetVisibilityOptions
{
    Published = true,
});
// vis.EventTypeId   -> "evt_456"
// vis.EventTypeName -> "order.paid"
// vis.Published     -> true
```

### Portal Sessions

```csharp
var session = await mgmt.PortalSessions.CreateAsync("ws_abc", "app_123", new CreatePortalSessionOptions
{
    Metadata = new Dictionary<string, string> { { "userId", "user-456" } },
});
// session.Url       -> redirect end-user here
// session.Code      -> one-time exchange code
// session.ExpiresAt -> expiration timestamp
```

### Deliveries

Read access to webhook delivery state, attempts, and (on Pro and above) the original decrypted payload.

```csharp
// Paginated list, newest-first. NextCursor is an opaque encrypted token —
// pass it back verbatim, do not decode or modify it.
var page = await mgmt.Deliveries.ListAsync("ws_abc", "ep_123", new ListDeliveriesOptions
{
    Limit = 50,
});
// page.Data       -> IReadOnlyList<Delivery>
// page.NextCursor -> string? (null when there are no more pages)

if (page.NextCursor != null)
{
    var next = await mgmt.Deliveries.ListAsync("ws_abc", "ep_123", new ListDeliveriesOptions
    {
        Cursor = page.NextCursor,
    });
}

// Filter by status
var failed = await mgmt.Deliveries.ListAsync("ws_abc", "ep_123", new ListDeliveriesOptions
{
    Status = "failed",
});

// Get a single delivery's status + metadata
var delivery = await mgmt.Deliveries.GetAsync("ws_abc", "del_xyz");

// Get a delivery with its decrypted payload. The response wraps the body in
// an envelope whose Status field describes whether the payload is available,
// gated by plan ("forbidden"), still in flight ("processing"), or absent.
var withPayload = await mgmt.Deliveries.GetAsync("ws_abc", "del_xyz", new GetDeliveryOptions
{
    IncludePayload = true,
});
if (withPayload.Payload?.Status == "available")
{
    // withPayload.Payload.Data is a JsonElement? carrying the original webhook body
    Console.WriteLine(withPayload.Payload.Data);
}

// List the attempt history for a delivery
var attempts = await mgmt.Deliveries.GetAttemptsAsync("ws_abc", "del_xyz");
```

---

## Error Handling

All SDK errors extend `NahookException`. Three specific types cover every failure mode:

```csharp
using Nahook;

try
{
    await client.SendAsync("ep_abc", new { OrderId = "123" });
}
catch (NahookApiException ex)
{
    // API returned an error response
    Console.WriteLine(ex.Status);       // 404
    Console.WriteLine(ex.Code);         // "not_found"
    Console.WriteLine(ex.Message);      // "Endpoint not found"
    Console.WriteLine(ex.RetryAfter);   // seconds (on 429s)

    // Convenience checks
    if (ex.IsRetryable) { /* 5xx or 429 */ }
    if (ex.IsAuthError) { /* 401 or 403 (token_disabled) */ }
    if (ex.IsNotFound) { /* 404 */ }
    if (ex.IsRateLimited) { /* 429 */ }
    if (ex.IsValidationError) { /* 400 */ }
}
catch (NahookNetworkException ex)
{
    // Network-level failure (DNS, connection refused, etc.)
    Console.WriteLine(ex.InnerException); // original exception
}
catch (NahookTimeoutException ex)
{
    // Request exceeded configured timeout
    Console.WriteLine(ex.TimeoutMs); // timeout that was exceeded
}
```

---

## Authentication

| Context | Token format | Header |
|---------|-------------|--------|
| Ingestion (`NahookClient`) | `nhk_{region}_{hex}` | `Authorization: Bearer nhk_...` |
| Management (`NahookManagement`) | `nhm_...` | `Authorization: Bearer nhm_...` |

API keys are region-aware. The SDK automatically routes to the correct regional API based on the key prefix (e.g., `nhk_us_...` routes to US, `nhk_eu_...` routes to EU).

---

## License

MIT
