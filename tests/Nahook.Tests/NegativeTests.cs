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

/// <summary>
/// Negative / resilience tests that verify the SDK handles malformed, empty,
/// and unexpected HTTP responses gracefully.
/// Uses TestHttpMessageHandler from NahookClientTests.cs.
/// </summary>
public sealed class NegativeTests
{
    /// <summary>
    /// Custom handler that allows setting the content type independently.
    /// </summary>
    private sealed class NegativeHttpMessageHandler : HttpMessageHandler
    {
        public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
        public string ResponseBody { get; set; } = "";
        public string ContentType { get; set; } = "application/json";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = new HttpResponseMessage(ResponseStatusCode);

            if (ResponseBody.Length > 0)
            {
                response.Content = new StringContent(ResponseBody, Encoding.UTF8, ContentType);
            }
            else
            {
                // Even for empty body, set the content type header
                response.Content = new StringContent("", Encoding.UTF8, ContentType);
            }

            return Task.FromResult(response);
        }
    }

    // NEG-01: Malformed JSON response on 200
    [Fact]
    public async Task NEG01_MalformedJson_on_200_throws_parse_error()
    {
        using var handler = new NegativeHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = "{invalid json!!!",
            ContentType = "application/json"
        };
        using var client = new NahookClient("nhk_us_test123", handler, new NahookClientOptions
        {
            BaseUrl = "https://test.nahook.com"
        });

        await Assert.ThrowsAsync<JsonException>(async () =>
        {
            await client.SendAsync("ep_1", new SendOptions
            {
                Payload = new Dictionary<string, object> { ["event"] = "test" }
            });
        });
    }

    // NEG-02: Empty body on 200
    [Fact]
    public async Task NEG02_EmptyBody_on_200_throws_parse_error()
    {
        using var handler = new NegativeHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = "",
            ContentType = "application/json"
        };
        using var client = new NahookClient("nhk_us_test123", handler, new NahookClientOptions
        {
            BaseUrl = "https://test.nahook.com"
        });

        await Assert.ThrowsAsync<JsonException>(async () =>
        {
            await client.SendAsync("ep_1", new SendOptions
            {
                Payload = new Dictionary<string, object> { ["event"] = "test" }
            });
        });
    }

    // NEG-03: 5xx with HTML body
    [Fact]
    public async Task NEG03_Html_5xx_throws_api_error_and_is_retryable()
    {
        using var handler = new NegativeHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.ServiceUnavailable,
            ResponseBody = "<html><body>Service Unavailable</body></html>",
            ContentType = "text/html"
        };
        using var client = new NahookClient("nhk_us_test123", handler, new NahookClientOptions
        {
            BaseUrl = "https://test.nahook.com"
        });

        var ex = await Assert.ThrowsAsync<NahookApiException>(async () =>
        {
            await client.SendAsync("ep_1", new SendOptions
            {
                Payload = new Dictionary<string, object> { ["event"] = "test" }
            });
        });

        Assert.Equal(503, ex.Status);
        Assert.True(ex.IsRetryable);
    }

    // NEG-04: 5xx with completely empty body
    [Fact]
    public async Task NEG04_Empty_5xx_throws_api_error_and_is_retryable()
    {
        using var handler = new NegativeHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.InternalServerError,
            ResponseBody = "",
            ContentType = "application/json"
        };
        using var client = new NahookClient("nhk_us_test123", handler, new NahookClientOptions
        {
            BaseUrl = "https://test.nahook.com"
        });

        var ex = await Assert.ThrowsAsync<NahookApiException>(async () =>
        {
            await client.SendAsync("ep_1", new SendOptions
            {
                Payload = new Dictionary<string, object> { ["event"] = "test" }
            });
        });

        Assert.Equal(500, ex.Status);
        Assert.True(ex.IsRetryable);
    }

    // NEG-05: Response with unknown extra fields is handled gracefully
    [Fact]
    public async Task NEG05_UnknownFields_handled_gracefully()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = JsonSerializer.Serialize(new[] {
                new {
                    id = "ep_1",
                    url = "https://example.com",
                    isActive = true,
                    type = "webhook",
                    unknownField = "should_be_ignored",
                    nested = new { deep = true },
                    createdAt = "2024-01-01",
                    updatedAt = "2024-01-01"
                }
            })
        };
        using var mgmt = new NahookManagement("nhm_test_abc", handler, new NahookManagementOptions
        {
            BaseUrl = "https://test.nahook.com"
        });

        var result = await mgmt.Endpoints.ListAsync("ws_123");

        Assert.NotEmpty(result.Data);
        Assert.Equal("ep_1", result.Data[0].Id);
    }

    // NEG-06: Response missing optional fields defaults gracefully
    [Fact]
    public async Task NEG06_MissingOptionalFields_defaults_gracefully()
    {
        using var handler = new TestHttpMessageHandler
        {
            ResponseStatusCode = HttpStatusCode.OK,
            ResponseBody = "[{\"id\":\"ep_1\"}]"
        };
        using var mgmt = new NahookManagement("nhm_test_abc", handler, new NahookManagementOptions
        {
            BaseUrl = "https://test.nahook.com"
        });

        var result = await mgmt.Endpoints.ListAsync("ws_123");

        Assert.NotEmpty(result.Data);
        Assert.Equal("ep_1", result.Data[0].Id);
        // Missing fields should default to empty/false, not throw
        Assert.Equal(string.Empty, result.Data[0].Url);
        Assert.False(result.Data[0].IsActive);
    }
}
