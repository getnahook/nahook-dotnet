using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Nahook;

internal sealed class NahookHttpClient : IDisposable
{
    private const string DefaultBaseUrl = "https://api.nahook.com";
    private const int DefaultTimeoutMs = 30_000;
    private const int BaseDelayMs = 500;
    private const int MaxDelayMs = 10_000;
    private const string SdkVersion = "0.1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly string _token;
    private readonly string _baseUrl;
    private readonly int _timeoutMs;
    private readonly int _retries;
    private readonly bool _ownsHttpClient;

    public NahookHttpClient(string token, string? baseUrl = null, int? timeoutMs = null, int? retries = null)
    {
        _token = token;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        _timeoutMs = timeoutMs ?? DefaultTimeoutMs;
        _retries = retries ?? 0;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"nahook-dotnet/{SdkVersion}");
        _ownsHttpClient = true;
    }

    // Internal constructor for testing — accepts a custom HttpMessageHandler.
    internal NahookHttpClient(string token, HttpMessageHandler handler, string? baseUrl = null, int? timeoutMs = null, int? retries = null)
    {
        _token = token;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        _timeoutMs = timeoutMs ?? DefaultTimeoutMs;
        _retries = retries ?? 0;

        _httpClient = new HttpClient(handler, disposeHandler: false);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"nahook-dotnet/{SdkVersion}");
        _ownsHttpClient = true;
    }

    public async Task<T?> RequestAsync<T>(HttpMethod method, string path, object? body = null, Dictionary<string, string>? query = null, CancellationToken ct = default)
    {
        var (_, result) = await ExecuteWithRetryAsync<T>(method, path, body, query, ct).ConfigureAwait(false);
        return result;
    }

    public async Task<(int StatusCode, T? Body)> RequestWithStatusAsync<T>(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        return await ExecuteWithRetryAsync<T>(method, path, body, null, ct).ConfigureAwait(false);
    }

    public async Task RequestAsync(HttpMethod method, string path, object? body = null, CancellationToken ct = default)
    {
        await ExecuteWithRetryAsync<object>(method, path, body, null, ct).ConfigureAwait(false);
    }

    private async Task<(int StatusCode, T? Body)> ExecuteWithRetryAsync<T>(HttpMethod method, string path, object? body, Dictionary<string, string>? query, CancellationToken ct)
    {
        int maxAttempts = _retries + 1;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await ExecuteOnceAsync<T>(method, path, body, query, ct).ConfigureAwait(false);
            }
            catch (NahookApiException ex) when (attempt < maxAttempts - 1 && ex.IsRetryable)
            {
                int delay = ComputeDelay(attempt, ex.RetryAfter);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (NahookNetworkException) when (attempt < maxAttempts - 1)
            {
                int delay = ComputeDelay(attempt, null);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (NahookTimeoutException) when (attempt < maxAttempts - 1)
            {
                int delay = ComputeDelay(attempt, null);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        // Unreachable: loop always returns or throws on the final attempt.
        throw new InvalidOperationException("Retry loop exited unexpectedly.");
    }

    private async Task<(int StatusCode, T? Body)> ExecuteOnceAsync<T>(HttpMethod method, string path, object? body, Dictionary<string, string>? query, CancellationToken ct)
    {
        string url = BuildUrl(path, query);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (body != null)
        {
            string json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_timeoutMs);

        try
        {
            response = await _httpClient.SendAsync(request, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new NahookTimeoutException(_timeoutMs);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new NahookNetworkException(ex);
        }

        int statusCode = (int)response.StatusCode;

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return (statusCode, default);
        }

        string responseBody = await response.Content.ReadAsStringAsync(
#if NET8_0_OR_GREATER
            ct
#endif
        ).ConfigureAwait(false);

        // 2xx or 207 — parse the result.
        if (response.IsSuccessStatusCode || statusCode == 207)
        {
            T? result = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
            return (statusCode, result);
        }

        // Error response — try to parse structured error.
        string errorCode = "unknown";
        string errorMessage = responseBody;
        int? retryAfter = null;

        try
        {
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseBody, JsonOptions);
            if (errorResponse?.Error != null)
            {
                errorCode = errorResponse.Error.Code;
                errorMessage = errorResponse.Error.Message;
            }
        }
        catch
        {
            // Use raw body as message if parsing fails.
        }

        if (response.Headers.TryGetValues("Retry-After", out var retryValues))
        {
            foreach (var val in retryValues)
            {
                if (int.TryParse(val, out int seconds))
                {
                    retryAfter = seconds;
                    break;
                }
            }
        }

        throw new NahookApiException(errorMessage, statusCode, errorCode, retryAfter);
    }

    private string BuildUrl(string path, Dictionary<string, string>? query)
    {
        var sb = new StringBuilder(_baseUrl);
        sb.Append(path);

        if (query != null && query.Count > 0)
        {
            sb.Append('?');
            bool first = true;
            foreach (var kvp in query)
            {
                if (!first) sb.Append('&');
                sb.Append(Uri.EscapeDataString(kvp.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(kvp.Value));
                first = false;
            }
        }

        return sb.ToString();
    }

    private static int ComputeDelay(int attempt, int? retryAfterSeconds)
    {
        if (retryAfterSeconds.HasValue && retryAfterSeconds.Value > 0)
        {
            return retryAfterSeconds.Value * 1000;
        }

        int exponentialDelay = BaseDelayMs * (1 << attempt);
        int cappedDelay = Math.Min(MaxDelayMs, exponentialDelay);
        // Full jitter: uniform random between 0 and cappedDelay.
        return Random.Shared.Next(0, cappedDelay + 1);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
