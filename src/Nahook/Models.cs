using System;
using System.Collections.Generic;
using System.Text.Json;
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

    /// <summary>
    /// Optional. Public id (e.g. <c>env_abc123</c>) of the environment to scope this endpoint.
    /// If omitted, the workspace's default environment is used.
    /// </summary>
    [JsonPropertyName("environmentId")]
    public string? EnvironmentId { get; set; }
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

    /// <summary>
    /// Maximum endpoints this application may have (disabled endpoints
    /// count). <c>null</c> means unlimited.
    /// </summary>
    [JsonPropertyName("maxEndpoints")]
    public int? MaxEndpoints { get; set; }

    /// <summary>
    /// Whether the Developer Portal exposes the event-type catalog to this
    /// application. Server default is <c>true</c>.
    /// </summary>
    [JsonPropertyName("showEventTypes")]
    public bool ShowEventTypes { get; set; } = true;

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

    /// <summary>
    /// Caps how many endpoints this application may have (disabled
    /// endpoints count). 0 makes the application read-only; <c>null</c>
    /// (omitted) means unlimited.
    /// </summary>
    [JsonPropertyName("maxEndpoints")]
    public int? MaxEndpoints { get; set; }

    /// <summary>
    /// Whether the Developer Portal exposes the event-type catalog.
    /// <c>null</c> (omitted) defaults to <c>true</c> server-side.
    /// </summary>
    [JsonPropertyName("showEventTypes")]
    public bool? ShowEventTypes { get; set; }
}

public sealed class UpdateApplicationOptions
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Tri-state: leave <c>null</c> to keep the current cap unchanged,
    /// set <see cref="NullableInt.OfNull"/> to clear it (unlimited), or
    /// <see cref="NullableInt.Of(int)"/> to set it.
    /// </summary>
    [JsonPropertyName("maxEndpoints")]
    public NullableInt? MaxEndpoints { get; set; }

    /// <summary>Omitted (unchanged) when <c>null</c>.</summary>
    [JsonPropertyName("showEventTypes")]
    public bool? ShowEventTypes { get; set; }
}

/// <summary>
/// A JSON field that serializes as either a number or an explicit null.
/// PATCH fields typed <see cref="NullableInt"/> are tri-state: a
/// <c>null</c> reference is omitted from the request body entirely (leave
/// unchanged), <see cref="OfNull"/> serializes as JSON null (clear), and
/// <see cref="Of(int)"/> serializes as the number (set).
/// </summary>
[JsonConverter(typeof(NullableIntJsonConverter))]
public sealed class NullableInt
{
    private NullableInt(int? value) => Value = value;

    /// <summary>The number to send; <c>null</c> serializes as JSON null.</summary>
    public int? Value { get; }

    /// <summary>Returns a <see cref="NullableInt"/> carrying <paramref name="value"/>.</summary>
    public static NullableInt Of(int value) => new(value);

    /// <summary>
    /// Returns a <see cref="NullableInt"/> that serializes as explicit JSON
    /// null — on <see cref="UpdateApplicationOptions.MaxEndpoints"/> this
    /// clears the cap (unlimited).
    /// </summary>
    public static NullableInt OfNull() => new(null);
}

internal sealed class NullableIntJsonConverter : JsonConverter<NullableInt>
{
    public override bool HandleNull => true;

    public override NullableInt? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Null ? NullableInt.OfNull() : NullableInt.Of(reader.GetInt32());
    }

    public override void Write(Utf8JsonWriter writer, NullableInt value, JsonSerializerOptions options)
    {
        if (value.Value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value.Value);
        }
    }
}

public sealed class Subscription
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("eventTypeId")]
    public string EventTypeId { get; set; } = string.Empty;

    [JsonPropertyName("eventTypeName")]
    public string? EventTypeName { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class CreateSubscriptionOptions
{
    [JsonPropertyName("eventTypeIds")]
    public List<string> EventTypeIds { get; set; } = new();
}

public sealed class CreateSubscriptionResult
{
    [JsonPropertyName("subscribed")]
    public int Subscribed { get; set; }
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

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("expiresInMinutes")]
    public int? ExpiresInMinutes { get; set; }
}

public sealed class Environment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

public sealed class CreateEnvironmentOptions
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
}

public sealed class UpdateEnvironmentOptions
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class EventTypeVisibility
{
    [JsonPropertyName("eventTypeId")]
    public string EventTypeId { get; set; } = string.Empty;

    [JsonPropertyName("eventTypeName")]
    public string EventTypeName { get; set; } = string.Empty;

    [JsonPropertyName("published")]
    public bool Published { get; set; }
}

public sealed class SetVisibilityOptions
{
    [JsonPropertyName("published")]
    public bool Published { get; set; }
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
// Deliveries
// ──────────────────────────────────────────────

/// <summary>
/// A webhook delivery record. Identifiers are public ids (prefixed). Timestamps
/// are ISO-8601 strings as returned by the API.
/// </summary>
public sealed class Delivery
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [JsonPropertyName("endpointId")]
    public string EndpointId { get; set; } = string.Empty;

    /// <summary>
    /// One of <c>pending</c>, <c>delivering</c>, <c>delivered</c>,
    /// <c>scheduled_retry</c>, <c>failed</c>, <c>dead_letter</c>.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("totalAttempts")]
    public int TotalAttempts { get; set; }

    [JsonPropertyName("firstAttemptAt")]
    public string? FirstAttemptAt { get; set; }

    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    [JsonPropertyName("nextRetryAt")]
    public string? NextRetryAt { get; set; }

    [JsonPropertyName("hasPayload")]
    public bool HasPayload { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;
}

/// <summary>
/// A single delivery attempt. <c>Status</c> is an opaque string emitted by the
/// worker (e.g. <c>success</c>, <c>failed</c>) — model as a string, not an enum,
/// because the set may evolve server-side.
/// </summary>
public sealed class DeliveryAttempt
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("attemptNumber")]
    public int AttemptNumber { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("responseStatusCode")]
    public int? ResponseStatusCode { get; set; }

    [JsonPropertyName("responseTimeMs")]
    public int? ResponseTimeMs { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>
/// Tagged envelope returned when fetching a delivery with
/// <c>includePayload=true</c>. <see cref="Status"/> describes whether the
/// payload was retrievable; <see cref="Data"/> and <see cref="ContentType"/>
/// are only populated when <c>Status == "available"</c>.
/// </summary>
/// <remarks>
/// Valid statuses: <c>available</c>, <c>forbidden</c>, <c>processing</c>,
/// <c>not_found</c>, <c>error</c>. These are surfaced unchanged from the API —
/// they are not exceptions and the SDK does not raise on them.
/// </remarks>
public sealed class PayloadEnvelope
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The decrypted webhook body. Only present when <see cref="Status"/> is
    /// <c>available</c>. Use <see cref="JsonElement"/> methods to inspect or
    /// deserialise into a concrete type.
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }

    /// <summary>
    /// MIME type of <see cref="Data"/>. Only present when <see cref="Status"/>
    /// is <c>available</c>. Currently always <c>application/json</c>.
    /// </summary>
    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }
}

/// <summary>
/// A <see cref="Delivery"/> optionally accompanied by a <see cref="PayloadEnvelope"/>.
/// The <see cref="Payload"/> field is only populated when the SDK call used
/// <c>includePayload=true</c>; otherwise it is <c>null</c>.
/// </summary>
public sealed class DeliveryWithPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("idempotencyKey")]
    public string IdempotencyKey { get; set; } = string.Empty;

    [JsonPropertyName("endpointId")]
    public string EndpointId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("totalAttempts")]
    public int TotalAttempts { get; set; }

    [JsonPropertyName("firstAttemptAt")]
    public string? FirstAttemptAt { get; set; }

    [JsonPropertyName("deliveredAt")]
    public string? DeliveredAt { get; set; }

    [JsonPropertyName("nextRetryAt")]
    public string? NextRetryAt { get; set; }

    [JsonPropertyName("hasPayload")]
    public bool HasPayload { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public PayloadEnvelope? Payload { get; set; }
}

/// <summary>
/// Generic cursor-paginated container. <see cref="NextCursor"/> is the opaque
/// token to pass back on the next call to fetch the next page, or <c>null</c>
/// when there are no more pages.
/// </summary>
public sealed record PaginatedResult<T>(IReadOnlyList<T> Data, string? NextCursor);

/// <summary>
/// Optional query parameters for listing deliveries scoped to an endpoint.
/// All fields are optional; omitted fields are not sent on the wire.
/// </summary>
public sealed class ListDeliveriesOptions
{
    /// <summary>Page size. Server default is 50, maximum 100.</summary>
    public int? Limit { get; set; }

    /// <summary>Opaque cursor returned by a previous <c>list()</c> call. Pass verbatim.</summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// Filter to a single status: <c>pending</c>, <c>delivering</c>,
    /// <c>delivered</c>, <c>scheduled_retry</c>, <c>failed</c>, <c>dead_letter</c>.
    /// </summary>
    public string? Status { get; set; }
}

/// <summary>
/// Optional query parameters for fetching a single delivery.
/// </summary>
public sealed class GetDeliveryOptions
{
    /// <summary>
    /// When <c>true</c>, the API returns the decrypted webhook body wrapped in
    /// a <see cref="PayloadEnvelope"/>. Plan-gated server-side.
    /// </summary>
    public bool? IncludePayload { get; set; }
}

// Internal wire shape for the paginated list response. We rename the array
// field from "deliveries" to PaginatedResult.Data at the SDK boundary so the
// container is generic across resources.
internal sealed class DeliveriesListResponse
{
    [JsonPropertyName("deliveries")]
    public List<Delivery>? Deliveries { get; set; }

    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
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
