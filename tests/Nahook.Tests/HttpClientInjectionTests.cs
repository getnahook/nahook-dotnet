using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nahook.Tests;

// Reflection helpers — peek at the SDK's owned HttpClient + its inner handler.
internal static class HttpClientReflection
{
    internal static HttpClient GetClientHttpClient(NahookClient client)
    {
        var httpField = typeof(NahookClient).GetField("_http", BindingFlags.NonPublic | BindingFlags.Instance);
        var http = httpField!.GetValue(client)!;
        var clientField = http.GetType().GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
        return (HttpClient)clientField!.GetValue(http)!;
    }

    internal static HttpClient GetMgmtHttpClient(NahookManagement mgmt)
    {
        var httpField = typeof(NahookManagement).GetField("_http", BindingFlags.NonPublic | BindingFlags.Instance);
        var http = httpField!.GetValue(mgmt)!;
        var clientField = http.GetType().GetField("_httpClient", BindingFlags.NonPublic | BindingFlags.Instance);
        return (HttpClient)clientField!.GetValue(http)!;
    }

    internal static HttpMessageHandler GetInnerHandler(HttpClient client)
    {
        // HttpClient stores its handler in a private `_handler` field on the base class HttpMessageInvoker.
        var field = typeof(HttpMessageInvoker).GetField("_handler", BindingFlags.NonPublic | BindingFlags.Instance);
        return (HttpMessageHandler)field!.GetValue(client)!;
    }
}

// ──────────────────────────────────────────────
// Pass 1: default SocketsHttpHandler config
// ──────────────────────────────────────────────

public sealed class DefaultHandlerTests
{
    [Fact]
    public void Client_default_handler_is_SocketsHttpHandler()
    {
        using var client = new NahookClient("nhk_us_test");
        var httpClient = HttpClientReflection.GetClientHttpClient(client);
        var handler = HttpClientReflection.GetInnerHandler(httpClient);

        Assert.IsType<SocketsHttpHandler>(handler);
    }

    [Fact]
    public void Client_default_handler_caps_PooledConnectionLifetime_at_5_minutes()
    {
        using var client = new NahookClient("nhk_us_test");
        var handler = (SocketsHttpHandler)HttpClientReflection.GetInnerHandler(
            HttpClientReflection.GetClientHttpClient(client));

        Assert.Equal(TimeSpan.FromMinutes(5), handler.PooledConnectionLifetime);
    }

    [Fact]
    public void Client_default_handler_sets_PooledConnectionIdleTimeout_to_2_minutes()
    {
        using var client = new NahookClient("nhk_us_test");
        var handler = (SocketsHttpHandler)HttpClientReflection.GetInnerHandler(
            HttpClientReflection.GetClientHttpClient(client));

        Assert.Equal(TimeSpan.FromMinutes(2), handler.PooledConnectionIdleTimeout);
    }

    [Fact]
    public void Client_default_handler_sets_MaxConnectionsPerServer_to_50()
    {
        using var client = new NahookClient("nhk_us_test");
        var handler = (SocketsHttpHandler)HttpClientReflection.GetInnerHandler(
            HttpClientReflection.GetClientHttpClient(client));

        Assert.Equal(50, handler.MaxConnectionsPerServer);
    }

    [Fact]
    public void Management_default_handler_is_SocketsHttpHandler()
    {
        using var mgmt = new NahookManagement("nhm_test");
        var httpClient = HttpClientReflection.GetMgmtHttpClient(mgmt);
        var handler = HttpClientReflection.GetInnerHandler(httpClient);

        Assert.IsType<SocketsHttpHandler>(handler);
    }
}

// ──────────────────────────────────────────────
// Pass 2: BYO HttpClient (caller-owned)
// ──────────────────────────────────────────────

public sealed class HttpClientInjectionTests
{
    [Fact]
    public void Client_HttpClient_option_is_used_verbatim()
    {
        using var customHandler = new TestHttpMessageHandler();
        var customClient = new HttpClient(customHandler);

        using var client = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            HttpClient = customClient
        });

        Assert.Same(customClient, HttpClientReflection.GetClientHttpClient(client));
        customClient.Dispose();
    }

    [Fact]
    public async Task Client_HttpClient_option_is_not_disposed_when_client_disposed()
    {
        var trackingHandler = new DisposeTrackingHandler();
        var customClient = new HttpClient(trackingHandler);

        var client = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            HttpClient = customClient,
            BaseUrl = "https://test.nahook.com"
        });

        client.Dispose();

        // Caller-owned HttpClient must remain usable after SDK disposal.
        Assert.False(trackingHandler.IsDisposed, "Caller-supplied HttpClient handler must not be disposed");
        var response = await customClient.GetAsync("https://test.nahook.com/anything");
        Assert.NotNull(response);

        customClient.Dispose();
        Assert.True(trackingHandler.IsDisposed);
    }

    [Fact]
    public async Task Client_HttpClient_option_Timeout_is_reflected_in_TimeoutException()
    {
        var slowHandler = new SlowHandler(TimeSpan.FromMilliseconds(500));
        using var customClient = new HttpClient(slowHandler) { Timeout = TimeSpan.FromMilliseconds(50) };

        using var client = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            HttpClient = customClient,
            BaseUrl = "https://test.nahook.com"
        });

        var ex = await Assert.ThrowsAsync<NahookTimeoutException>(async () =>
            await client.SendAsync("ep_1", new SendOptions
            {
                Payload = new Dictionary<string, object>()
            })
        );

        Assert.Equal(50, ex.TimeoutMs);
    }

    [Fact]
    public void Management_HttpClient_option_is_used_verbatim()
    {
        using var customHandler = new TestHttpMessageHandler();
        var customClient = new HttpClient(customHandler);

        using var mgmt = new NahookManagement("nhm_test", new NahookManagementOptions
        {
            HttpClient = customClient
        });

        Assert.Same(customClient, HttpClientReflection.GetMgmtHttpClient(mgmt));
        customClient.Dispose();
    }

    [Fact]
    public void Management_HttpClient_option_is_not_disposed_when_mgmt_disposed()
    {
        var trackingHandler = new DisposeTrackingHandler();
        var customClient = new HttpClient(trackingHandler);

        var mgmt = new NahookManagement("nhm_test", new NahookManagementOptions
        {
            HttpClient = customClient
        });

        mgmt.Dispose();

        Assert.False(trackingHandler.IsDisposed);
        customClient.Dispose();
    }
}

// ──────────────────────────────────────────────
// Pass 2: BYO Handler (SDK wraps in HttpClient, caller-owns handler)
// ──────────────────────────────────────────────

public sealed class HandlerInjectionTests
{
    [Fact]
    public void Client_Handler_option_is_wrapped_into_HttpClient()
    {
        using var customHandler = new TestHttpMessageHandler();

        using var client = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            Handler = customHandler
        });

        var actual = HttpClientReflection.GetInnerHandler(
            HttpClientReflection.GetClientHttpClient(client));
        Assert.Same(customHandler, actual);
    }

    [Fact]
    public void Client_Handler_option_is_not_disposed_when_client_disposed()
    {
        var trackingHandler = new DisposeTrackingHandler();

        var client = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            Handler = trackingHandler,
            BaseUrl = "https://test.nahook.com"
        });

        client.Dispose();

        Assert.False(trackingHandler.IsDisposed,
            "Caller-supplied handler must not be disposed by NahookClient.Dispose()");

        trackingHandler.Dispose();
    }

    [Fact]
    public void Client_HttpClient_option_takes_precedence_over_Handler_option()
    {
        using var customHandler = new TestHttpMessageHandler();
        var customClient = new HttpClient(new TestHttpMessageHandler());

        using var client = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            HttpClient = customClient,
            Handler = customHandler
        });

        Assert.Same(customClient, HttpClientReflection.GetClientHttpClient(client));
        customClient.Dispose();
    }

    [Fact]
    public void Management_Handler_option_is_wrapped_into_HttpClient()
    {
        using var customHandler = new TestHttpMessageHandler();

        using var mgmt = new NahookManagement("nhm_test", new NahookManagementOptions
        {
            Handler = customHandler
        });

        var actual = HttpClientReflection.GetInnerHandler(
            HttpClientReflection.GetMgmtHttpClient(mgmt));
        Assert.Same(customHandler, actual);
    }

    [Fact]
    public void Management_Handler_option_is_not_disposed_when_mgmt_disposed()
    {
        var trackingHandler = new DisposeTrackingHandler();

        var mgmt = new NahookManagement("nhm_test", new NahookManagementOptions
        {
            Handler = trackingHandler
        });

        mgmt.Dispose();

        Assert.False(trackingHandler.IsDisposed);
        trackingHandler.Dispose();
    }
}

// ──────────────────────────────────────────────
// HttpClient is not reconstructed per request
// ──────────────────────────────────────────────

public sealed class HttpClientReuseTests
{
    [Fact]
    public async Task SDK_does_not_reconstruct_HttpClient_per_request()
    {
        var counter = new CountingHandler();
        using var client = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            Handler = counter,
            BaseUrl = "https://test.nahook.com"
        });

        // Snapshot HttpClient identity before any calls.
        var before = HttpClientReflection.GetClientHttpClient(client);

        for (int i = 0; i < 5; i++)
        {
            await client.SendAsync("ep_abc", new SendOptions
            {
                Payload = new Dictionary<string, object> { ["i"] = i }
            });
        }

        Assert.Equal(5, counter.RequestCount);
        // Same HttpClient instance after 5 sends — SDK didn't swap it underneath.
        // (Proves the SDK-level contract; TCP socket reuse would need an integration test.)
        Assert.Same(before, HttpClientReflection.GetClientHttpClient(client));
    }
}

// ──────────────────────────────────────────────
// User-Agent semantics — must not mutate caller-owned HttpClient
// ──────────────────────────────────────────────

public sealed class UserAgentTests
{
    [Fact]
    public void Caller_owned_HttpClient_DefaultRequestHeaders_UserAgent_is_not_mutated()
    {
        var customClient = new HttpClient(new TestHttpMessageHandler());
        Assert.Empty(customClient.DefaultRequestHeaders.UserAgent);

        // Two NahookClient instances over the same shared HttpClient — repeated
        // construction must not accumulate UA entries on the caller's object.
        var nahook1 = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            HttpClient = customClient
        });
        var nahook2 = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            HttpClient = customClient
        });

        Assert.Empty(customClient.DefaultRequestHeaders.UserAgent);

        nahook1.Dispose();
        nahook2.Dispose();
        customClient.Dispose();
    }

    [Fact]
    public async Task Caller_owned_HttpClient_still_sends_user_agent_on_each_request()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseBody = JsonSerializer.Serialize(new
            {
                deliveryId = "del_1",
                idempotencyKey = "k",
                status = "accepted"
            })
        };
        var customClient = new HttpClient(handler);
        using var client = new NahookClient("nhk_us_test", new NahookClientOptions
        {
            HttpClient = customClient,
            BaseUrl = "https://test.nahook.com"
        });

        await client.SendAsync("ep_1", new SendOptions
        {
            Payload = new Dictionary<string, object>()
        });

        Assert.Contains(handler.LastRequest!.Headers.UserAgent,
            p => p.Product != null && p.Product.Name == "nahook-dotnet");

        customClient.Dispose();
    }
}

// ──────────────────────────────────────────────
// Shared test fakes
// ──────────────────────────────────────────────

internal sealed class SlowHandler : HttpMessageHandler
{
    private readonly TimeSpan _delay;

    public SlowHandler(TimeSpan delay)
    {
        _delay = delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await Task.Delay(_delay, ct).ConfigureAwait(false);
        return new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
    }
}

internal sealed class DisposeTrackingHandler : HttpMessageHandler
{
    public bool IsDisposed { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
    }

    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}

internal sealed class CountingHandler : HttpMessageHandler
{
    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        RequestCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                deliveryId = "del_" + RequestCount,
                idempotencyKey = "k-" + RequestCount,
                status = "accepted"
            }), Encoding.UTF8, "application/json")
        });
    }
}
