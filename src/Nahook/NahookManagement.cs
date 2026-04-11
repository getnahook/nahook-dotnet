using System;
using System.Net.Http;

namespace Nahook;

/// <summary>
/// Management client for administering Nahook workspaces, endpoints, event types, etc.
/// Uses management tokens with the <c>nhm_</c> prefix.
/// </summary>
public sealed class NahookManagement : IDisposable
{
    private readonly NahookHttpClient _http;

    /// <summary>
    /// Creates a new management client.
    /// </summary>
    /// <param name="token">Management token (must start with <c>nhm_</c>).</param>
    /// <param name="options">Optional client configuration.</param>
    public NahookManagement(string token, NahookManagementOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Management token must not be empty.", nameof(token));
        if (!token.StartsWith("nhm_", StringComparison.Ordinal))
            throw new ArgumentException("Management token must start with 'nhm_'.", nameof(token));

        _http = new NahookHttpClient(
            token: token,
            baseUrl: options?.BaseUrl,
            timeoutMs: options?.TimeoutMs,
            retries: 0 // Management client never retries.
        );

        Endpoints = new EndpointsResource(_http);
        EventTypes = new EventTypesResource(_http);
        Applications = new ApplicationsResource(_http);
        Subscriptions = new SubscriptionsResource(_http);
        PortalSessions = new PortalSessionsResource(_http);
        Environments = new EnvironmentsResource(_http);
    }

    // Internal constructor for testing with a custom handler.
    internal NahookManagement(string token, HttpMessageHandler handler, NahookManagementOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Management token must not be empty.", nameof(token));
        if (!token.StartsWith("nhm_", StringComparison.Ordinal))
            throw new ArgumentException("Management token must start with 'nhm_'.", nameof(token));

        _http = new NahookHttpClient(
            token: token,
            handler: handler,
            baseUrl: options?.BaseUrl,
            timeoutMs: options?.TimeoutMs,
            retries: 0
        );

        Endpoints = new EndpointsResource(_http);
        EventTypes = new EventTypesResource(_http);
        Applications = new ApplicationsResource(_http);
        Subscriptions = new SubscriptionsResource(_http);
        PortalSessions = new PortalSessionsResource(_http);
        Environments = new EnvironmentsResource(_http);
    }

    public EndpointsResource Endpoints { get; }
    public EventTypesResource EventTypes { get; }
    public ApplicationsResource Applications { get; }
    public SubscriptionsResource Subscriptions { get; }
    public PortalSessionsResource PortalSessions { get; }
    public EnvironmentsResource Environments { get; }

    public void Dispose()
    {
        _http.Dispose();
    }
}

/// <summary>
/// Configuration options for <see cref="NahookManagement"/>.
/// </summary>
public sealed class NahookManagementOptions
{
    /// <summary>Base URL of the Nahook API. Defaults to https://api.nahook.com.</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Request timeout in milliseconds. Defaults to 30000.</summary>
    public int? TimeoutMs { get; init; }
}
