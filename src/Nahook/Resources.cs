using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nahook;

/// <summary>
/// Manages endpoints within a workspace.
/// </summary>
public sealed class EndpointsResource
{
    private readonly NahookHttpClient _http;
    internal EndpointsResource(NahookHttpClient http) => _http = http;

    public async Task<ListResult<Endpoint>> ListAsync(string workspaceId, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/endpoints";
        var data = await _http.RequestAsync<List<Endpoint>>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return new ListResult<Endpoint> { Data = data ?? new List<Endpoint>() };
    }

    public async Task<Endpoint> CreateAsync(string workspaceId, CreateEndpointOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/endpoints";
        var result = await _http.RequestAsync<Endpoint>(HttpMethod.Post, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<Endpoint> GetAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/endpoints/{Uri.EscapeDataString(id)}";
        var result = await _http.RequestAsync<Endpoint>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<Endpoint> UpdateAsync(string workspaceId, string id, UpdateEndpointOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/endpoints/{Uri.EscapeDataString(id)}";
        var result = await _http.RequestAsync<Endpoint>(HttpMethod.Patch, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task DeleteAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/endpoints/{Uri.EscapeDataString(id)}";
        await _http.RequestAsync(HttpMethod.Delete, path, ct: ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Manages event types within a workspace.
/// </summary>
public sealed class EventTypesResource
{
    private readonly NahookHttpClient _http;
    internal EventTypesResource(NahookHttpClient http) => _http = http;

    public async Task<ListResult<EventType>> ListAsync(string workspaceId, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/event-types";
        var data = await _http.RequestAsync<List<EventType>>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return new ListResult<EventType> { Data = data ?? new List<EventType>() };
    }

    public async Task<EventType> CreateAsync(string workspaceId, CreateEventTypeOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/event-types";
        var result = await _http.RequestAsync<EventType>(HttpMethod.Post, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<EventType> GetAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/event-types/{Uri.EscapeDataString(id)}";
        var result = await _http.RequestAsync<EventType>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<EventType> UpdateAsync(string workspaceId, string id, UpdateEventTypeOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/event-types/{Uri.EscapeDataString(id)}";
        var result = await _http.RequestAsync<EventType>(HttpMethod.Patch, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task DeleteAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/event-types/{Uri.EscapeDataString(id)}";
        await _http.RequestAsync(HttpMethod.Delete, path, ct: ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Manages applications within a workspace.
/// </summary>
public sealed class ApplicationsResource
{
    private readonly NahookHttpClient _http;
    internal ApplicationsResource(NahookHttpClient http) => _http = http;

    public async Task<ListResult<Application>> ListAsync(string workspaceId, ListOptions? options = null, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/applications";
        Dictionary<string, string>? query = null;

        if (options != null)
        {
            query = new Dictionary<string, string>();
            if (options.Limit.HasValue) query["limit"] = options.Limit.Value.ToString();
            if (options.Offset.HasValue) query["offset"] = options.Offset.Value.ToString();
        }

        var data = await _http.RequestAsync<List<Application>>(HttpMethod.Get, path, query: query, ct: ct).ConfigureAwait(false);
        return new ListResult<Application> { Data = data ?? new List<Application>() };
    }

    public async Task<Application> CreateAsync(string workspaceId, CreateApplicationOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/applications";
        var result = await _http.RequestAsync<Application>(HttpMethod.Post, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<Application> GetAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(id)}";
        var result = await _http.RequestAsync<Application>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<Application> UpdateAsync(string workspaceId, string id, UpdateApplicationOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(id)}";
        var result = await _http.RequestAsync<Application>(HttpMethod.Patch, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task DeleteAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(id)}";
        await _http.RequestAsync(HttpMethod.Delete, path, ct: ct).ConfigureAwait(false);
    }

    public async Task<ListResult<Endpoint>> ListEndpointsAsync(string workspaceId, string appId, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(appId)}/endpoints";
        var data = await _http.RequestAsync<List<Endpoint>>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return new ListResult<Endpoint> { Data = data ?? new List<Endpoint>() };
    }

    public async Task<Endpoint> CreateEndpointAsync(string workspaceId, string appId, CreateEndpointOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(appId)}/endpoints";
        var result = await _http.RequestAsync<Endpoint>(HttpMethod.Post, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }
}

/// <summary>
/// Manages subscriptions on endpoints within a workspace.
/// </summary>
public sealed class SubscriptionsResource
{
    private readonly NahookHttpClient _http;
    internal SubscriptionsResource(NahookHttpClient http) => _http = http;

    public async Task<ListResult<Subscription>> ListAsync(string workspaceId, string endpointId, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/endpoints/{Uri.EscapeDataString(endpointId)}/subscriptions";
        var data = await _http.RequestAsync<List<Subscription>>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return new ListResult<Subscription> { Data = data ?? new List<Subscription>() };
    }

    public async Task<CreateSubscriptionResult> CreateAsync(string workspaceId, string endpointId, CreateSubscriptionOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/endpoints/{Uri.EscapeDataString(endpointId)}/subscriptions";
        var result = await _http.RequestAsync<CreateSubscriptionResult>(HttpMethod.Post, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task DeleteAsync(string workspaceId, string endpointId, string eventTypeId, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/endpoints/{Uri.EscapeDataString(endpointId)}/subscriptions/{Uri.EscapeDataString(eventTypeId)}";
        await _http.RequestAsync(HttpMethod.Delete, path, ct: ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Manages environments within a workspace.
/// </summary>
public sealed class EnvironmentsResource
{
    private readonly NahookHttpClient _http;
    internal EnvironmentsResource(NahookHttpClient http) => _http = http;

    public async Task<ListResult<Environment>> ListAsync(string workspaceId, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/environments";
        var data = await _http.RequestAsync<List<Environment>>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return new ListResult<Environment> { Data = data ?? new List<Environment>() };
    }

    public async Task<Environment> CreateAsync(string workspaceId, CreateEnvironmentOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/environments";
        var result = await _http.RequestAsync<Environment>(HttpMethod.Post, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<Environment> GetAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/environments/{Uri.EscapeDataString(id)}";
        var result = await _http.RequestAsync<Environment>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task<Environment> UpdateAsync(string workspaceId, string id, UpdateEnvironmentOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/environments/{Uri.EscapeDataString(id)}";
        var result = await _http.RequestAsync<Environment>(HttpMethod.Patch, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }

    public async Task DeleteAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/environments/{Uri.EscapeDataString(id)}";
        await _http.RequestAsync(HttpMethod.Delete, path, ct: ct).ConfigureAwait(false);
    }

    public async Task<ListResult<EventTypeVisibility>> ListEventTypeVisibilityAsync(string workspaceId, string envId, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/environments/{Uri.EscapeDataString(envId)}/event-types";
        var data = await _http.RequestAsync<List<EventTypeVisibility>>(HttpMethod.Get, path, ct: ct).ConfigureAwait(false);
        return new ListResult<EventTypeVisibility> { Data = data ?? new List<EventTypeVisibility>() };
    }

    public async Task<EventTypeVisibility> SetEventTypeVisibilityAsync(string workspaceId, string envId, string eventTypeId, SetVisibilityOptions options, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/environments/{Uri.EscapeDataString(envId)}/event-types/{Uri.EscapeDataString(eventTypeId)}/visibility";
        var result = await _http.RequestAsync<EventTypeVisibility>(HttpMethod.Put, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }
}

/// <summary>
/// Creates portal sessions for applications within a workspace.
/// </summary>
public sealed class PortalSessionsResource
{
    private readonly NahookHttpClient _http;
    internal PortalSessionsResource(NahookHttpClient http) => _http = http;

    public async Task<PortalSession> CreateAsync(string workspaceId, string appId, CreatePortalSessionOptions? options = null, CancellationToken ct = default)
    {
        string path = $"/management/v1/workspaces/{Uri.EscapeDataString(workspaceId)}/applications/{Uri.EscapeDataString(appId)}/portal";
        var result = await _http.RequestAsync<PortalSession>(HttpMethod.Post, path, options, ct: ct).ConfigureAwait(false);
        return result!;
    }
}
