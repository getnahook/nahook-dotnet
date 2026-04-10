using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Nahook;

// ──────────────────────────────────────────────
// Client (Ingestion) Models
// ──────────────────────────────────────────────

public sealed class SendOptions
{
    [JsonPropertyName("payload")]
    public Dictionary<string, object> Payload { get; set; } = new();

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }
}

public sealed class SendResult
{
    [JsonPropertyName("deliveryId")]
    public string DeliveryId { get; set; } = string.Empty;

    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public sealed class TriggerOptions
{
    [JsonPropertyName("payload")]
    public Dictionary<string, object> Payload { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class TriggerResult
{
    [JsonPropertyName("eventTypeId")]
    public string EventTypeId { get; set; } = string.Empty;

    [JsonPropertyName("deliveryIds")]
    public List<string> DeliveryIds { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

public sealed class SendBatchItem
{
    [JsonPropertyName("endpointId")]
    public string EndpointId { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public Dictionary<string, object> Payload { get; set; } = new();

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }
}

public sealed class TriggerBatchItem
{
    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public Dictionary<string, object> Payload { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class BatchResult
{
    [JsonPropertyName("items")]
    public List<BatchResultItem> Items { get; set; } = new();
}

public sealed class BatchResultItem
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("deliveryId")]
    public string? DeliveryId { get; set; }

    [JsonPropertyName("idempotencyKey")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("eventTypeId")]
    public string? EventTypeId { get; set; }

    [JsonPropertyName("deliveryIds")]
    public List<string>? DeliveryIds { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error")]
    public BatchItemError? Error { get; set; }
}

public sealed class BatchItemError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────
// Management Models
// ──────────────────────────────────────────────

public sealed class Endpoint
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("config")]
    public Dictionary<string, object>? Config { get; set; }

    [JsonPropertyName("secret")]
    public string? Secret { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

public sealed class CreateEndpointOptions
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("config")]
    public Dictionary<string, object>? Config { get; set; }

    [JsonPropertyName("authUsername")]
    public string? AuthUsername { get; set; }

    [JsonPropertyName("authPassword")]
    public string? AuthPassword { get; set; }
}

public sealed class UpdateEndpointOptions
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("isActive")]
    public bool? IsActive { get; set; }
}

public sealed class EventType
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class CreateEventTypeOptions
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class UpdateEventTypeOptions
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class Application
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

public sealed class CreateApplicationOptions
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class UpdateApplicationOptions
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class Subscription
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("endpointId")]
    public string EndpointId { get; set; } = string.Empty;

    [JsonPropertyName("eventTypeId")]
    public string EventTypeId { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class CreateSubscriptionOptions
{
    [JsonPropertyName("eventTypeId")]
    public string EventTypeId { get; set; } = string.Empty;
}

public sealed class PortalSession
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public string ExpiresAt { get; set; } = string.Empty;
}

public sealed class CreatePortalSessionOptions
{
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
}

public sealed class ListResult<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();
}

public sealed class ListOptions
{
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

// ──────────────────────────────────────────────
// Internal error response model
// ──────────────────────────────────────────────

internal sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public ErrorDetail Error { get; set; } = new();
}

internal sealed class ErrorDetail
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
