using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nahook;

/// <summary>
/// Ingestion client for sending webhooks via the Nahook API.
/// Uses API keys with the <c>nhk_</c> prefix.
/// </summary>
public sealed class NahookClient : IDisposable
{
    private readonly NahookHttpClient _http;

    /// <summary>
    /// Creates a new ingestion client.
    /// </summary>
    /// <param name="apiKey">API key (must start with <c>nhk_</c>).</param>
    /// <param name="options">Optional client configuration.</param>
    public NahookClient(string apiKey, NahookClientOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key must not be empty.", nameof(apiKey));
        if (!apiKey.StartsWith("nhk_", StringComparison.Ordinal))
            throw new ArgumentException("API key must start with 'nhk_'.", nameof(apiKey));

        _http = new NahookHttpClient(
            token: apiKey,
            baseUrl: options?.BaseUrl,
            timeoutMs: options?.TimeoutMs,
            retries: options?.Retries ?? 0,
            httpClient: options?.HttpClient,
            handler: options?.Handler
        );
    }

    // Internal constructor for testing with a custom handler. Delegates to the
    // public path via Options.Handler so resolution + ownership semantics live
    // in exactly one place.
    internal NahookClient(string apiKey, HttpMessageHandler handler, NahookClientOptions? options = null)
        : this(apiKey, new NahookClientOptions
        {
            BaseUrl = options?.BaseUrl,
            TimeoutMs = options?.TimeoutMs,
            Retries = options?.Retries,
            Handler = handler,
        })
    {
    }

    /// <summary>
    /// Sends a webhook to a specific endpoint.
    /// </summary>
    public async Task<SendResult> SendAsync(string endpointId, SendOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
            throw new ArgumentException("Endpoint ID must not be empty.", nameof(endpointId));

        string idempotencyKey = options.IdempotencyKey ?? Guid.NewGuid().ToString();

        var body = new { payload = options.Payload, idempotencyKey };
        string path = $"/api/ingest/{Uri.EscapeDataString(endpointId)}";

        var result = await _http.RequestAsync<SendResult>(HttpMethod.Post, path, body, ct: ct).ConfigureAwait(false);
        return result!;
    }

    /// <summary>
    /// Triggers an event type, delivering to all subscribed endpoints.
    /// </summary>
    public async Task<TriggerResult> TriggerAsync(string eventType, TriggerOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("Event type must not be empty.", nameof(eventType));

        var body = new { payload = options.Payload, metadata = options.Metadata };
        string path = $"/api/ingest/event/{Uri.EscapeDataString(eventType)}";

        var result = await _http.RequestAsync<TriggerResult>(HttpMethod.Post, path, body, ct: ct).ConfigureAwait(false);
        return result!;
    }

    /// <summary>
    /// Sends a batch of webhooks to specific endpoints.
    /// </summary>
    public async Task<BatchResult> SendBatchAsync(IEnumerable<SendBatchItem> items, CancellationToken ct = default)
    {
        var body = new { items = items.ToList() };
        var (_, result) = await _http.RequestWithStatusAsync<BatchResult>(HttpMethod.Post, "/api/ingest/batch", body, ct).ConfigureAwait(false);
        return result!;
    }

    /// <summary>
    /// Triggers a batch of event types.
    /// </summary>
    public async Task<BatchResult> TriggerBatchAsync(IEnumerable<TriggerBatchItem> items, CancellationToken ct = default)
    {
        var body = new { items = items.ToList() };
        var (_, result) = await _http.RequestWithStatusAsync<BatchResult>(HttpMethod.Post, "/api/ingest/event/batch", body, ct).ConfigureAwait(false);
        return result!;
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}

/// <summary>
/// Configuration options for <see cref="NahookClient"/>.
/// </summary>
public sealed class NahookClientOptions
{
    /// <summary>Base URL of the Nahook API. Defaults to https://api.nahook.com.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Request timeout in milliseconds. Defaults to 30000.</summary>
    public int? TimeoutMs { get; init; }

    /// <summary>Number of retries for retryable errors. Defaults to 0.</summary>
    public int? Retries { get; init; }

    /// <summary>
    /// Optional <see cref="System.Net.Http.HttpClient"/> to use for all requests. When supplied,
    /// the SDK uses it verbatim and will NOT dispose it on <see cref="NahookClient.Dispose"/> —
    /// the caller owns disposal. Use this to integrate with <c>IHttpClientFactory</c>, Polly,
    /// or any handler pipeline configured at the DI level. The caller-set <c>HttpClient.Timeout</c>
    /// governs request timeouts and is reported by <see cref="NahookTimeoutException"/>.
    /// </summary>
    public HttpClient? HttpClient { get; init; }

    /// <summary>
    /// Optional <see cref="HttpMessageHandler"/> wrapped into the SDK-owned <see cref="System.Net.Http.HttpClient"/>.
    /// The SDK disposes the wrapping <c>HttpClient</c> on <see cref="NahookClient.Dispose"/> but
    /// NOT the supplied handler — the caller owns the handler's lifecycle. Ignored when
    /// <see cref="HttpClient"/> is also supplied.
    /// </summary>
    public HttpMessageHandler? Handler { get; init; }
}
