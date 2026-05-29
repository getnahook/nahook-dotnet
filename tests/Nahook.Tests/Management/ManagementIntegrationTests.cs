using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Nahook.Tests.Management;

/// <summary>
/// Management API integration tests that hit a real Nahook API instance.
/// Skipped automatically when NAHOOK_TEST_API_URL or NAHOOK_TEST_MGMT_TOKEN is not set.
/// </summary>
public sealed class ManagementIntegrationTests : IDisposable
{
    private static readonly string? _apiUrl;
    private static readonly string? _token;
    private static readonly string? _workspaceId;
    private static readonly bool _skip;

    private readonly NahookManagement? _client;

    static ManagementIntegrationTests()
    {
        _apiUrl = System.Environment.GetEnvironmentVariable("NAHOOK_TEST_API_URL");
        _token = System.Environment.GetEnvironmentVariable("NAHOOK_TEST_MGMT_TOKEN");
        _workspaceId = System.Environment.GetEnvironmentVariable("NAHOOK_TEST_WORKSPACE_ID");

        _skip = string.IsNullOrEmpty(_apiUrl)
             || string.IsNullOrEmpty(_token)
             || string.IsNullOrEmpty(_workspaceId);
    }

    public ManagementIntegrationTests()
    {
        if (!_skip)
        {
            _client = new NahookManagement(_token!, new NahookManagementOptions { BaseUrl = _apiUrl });
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private static string Uid() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

    // ──────────────────────────────────────────────
    // EventTypes CRUD
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "ManagementIntegration")]
    public async Task EventTypes_Crud()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var uid = Uid();
        var name = $"mgmt.test.{uid}";

        // Create
        var created = await _client!.EventTypes.CreateAsync(_workspaceId!, new CreateEventTypeOptions
        {
            Name = name,
            Description = $"Integration test event type {uid}"
        });

        Assert.False(string.IsNullOrEmpty(created.Id));
        Assert.Equal(name, created.Name);

        try
        {
            // List
            var list = await _client!.EventTypes.ListAsync(_workspaceId!);
            Assert.Contains(list.Data, et => et.Id == created.Id);

            // Get
            var fetched = await _client!.EventTypes.GetAsync(_workspaceId!, created.Id);
            Assert.Equal(created.Id, fetched.Id);
            Assert.Equal(name, fetched.Name);

            // Update
            var updatedDesc = $"Updated {uid}";
            var updated = await _client!.EventTypes.UpdateAsync(_workspaceId!, created.Id, new UpdateEventTypeOptions
            {
                Description = updatedDesc
            });
            Assert.Equal(updatedDesc, updated.Description);
        }
        finally
        {
            // Delete (cleanup)
            await _client!.EventTypes.DeleteAsync(_workspaceId!, created.Id);
        }

        // Verify deletion
        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            _client!.EventTypes.GetAsync(_workspaceId!, created.Id));
        Assert.Equal(404, ex.Status);
    }

    // ──────────────────────────────────────────────
    // Endpoints CRUD
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "ManagementIntegration")]
    public async Task Endpoints_Crud()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var uid = Uid();

        // Create
        var created = await _client!.Endpoints.CreateAsync(_workspaceId!, new CreateEndpointOptions
        {
            Url = $"https://example.com/hooks/test-{uid}",
            Description = $"Integration test endpoint {uid}"
        });

        Assert.False(string.IsNullOrEmpty(created.Id));
        Assert.StartsWith("ep_", created.Id);
        Assert.True(created.IsActive);

        try
        {
            // List
            var list = await _client!.Endpoints.ListAsync(_workspaceId!);
            Assert.Contains(list.Data, ep => ep.Id == created.Id);

            // Get
            var fetched = await _client!.Endpoints.GetAsync(_workspaceId!, created.Id);
            Assert.Equal(created.Id, fetched.Id);
            Assert.Contains($"test-{uid}", fetched.Url);

            // Update
            var updatedDesc = $"Updated endpoint {uid}";
            var updated = await _client!.Endpoints.UpdateAsync(_workspaceId!, created.Id, new UpdateEndpointOptions
            {
                Description = updatedDesc,
                IsActive = false
            });
            Assert.Equal(updatedDesc, updated.Description);
            Assert.False(updated.IsActive);
        }
        finally
        {
            // Delete (cleanup)
            await _client!.Endpoints.DeleteAsync(_workspaceId!, created.Id);
        }

        // Verify deletion
        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            _client!.Endpoints.GetAsync(_workspaceId!, created.Id));
        Assert.Equal(404, ex.Status);
    }

    // ──────────────────────────────────────────────
    // Applications CRUD
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "ManagementIntegration")]
    public async Task Applications_Crud()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var uid = Uid();

        // Create
        var created = await _client!.Applications.CreateAsync(_workspaceId!, new CreateApplicationOptions
        {
            Name = $"Test App {uid}",
            ExternalId = $"ext-{uid}",
            Metadata = new Dictionary<string, string> { ["env"] = "test" }
        });

        Assert.False(string.IsNullOrEmpty(created.Id));
        Assert.Equal($"Test App {uid}", created.Name);
        Assert.Equal($"ext-{uid}", created.ExternalId);

        try
        {
            // List
            var list = await _client!.Applications.ListAsync(_workspaceId!);
            Assert.Contains(list.Data, app => app.Id == created.Id);

            // Get
            var fetched = await _client!.Applications.GetAsync(_workspaceId!, created.Id);
            Assert.Equal(created.Id, fetched.Id);
            Assert.Equal($"Test App {uid}", fetched.Name);

            // Update
            var updatedName = $"Updated App {uid}";
            var updated = await _client!.Applications.UpdateAsync(_workspaceId!, created.Id, new UpdateApplicationOptions
            {
                Name = updatedName,
                Metadata = new Dictionary<string, string> { ["env"] = "test", ["updated"] = "true" }
            });
            Assert.Equal(updatedName, updated.Name);
        }
        finally
        {
            // Delete (cleanup)
            await _client!.Applications.DeleteAsync(_workspaceId!, created.Id);
        }

        // Verify deletion
        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            _client!.Applications.GetAsync(_workspaceId!, created.Id));
        Assert.Equal(404, ex.Status);
    }

    // ──────────────────────────────────────────────
    // Subscriptions Lifecycle
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "ManagementIntegration")]
    public async Task Subscriptions_Lifecycle()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var uid = Uid();

        // Create supporting resources: endpoint + event type
        var endpoint = await _client!.Endpoints.CreateAsync(_workspaceId!, new CreateEndpointOptions
        {
            Url = $"https://example.com/hooks/sub-{uid}",
            Description = $"Subscription test endpoint {uid}"
        });

        var eventType = await _client!.EventTypes.CreateAsync(_workspaceId!, new CreateEventTypeOptions
        {
            Name = $"sub.test.{uid}",
            Description = $"Subscription test event type {uid}"
        });

        try
        {
            // Subscribe (plural eventTypeIds, returns { subscribed: N })
            var result = await _client!.Subscriptions.CreateAsync(_workspaceId!, endpoint.Id, new CreateSubscriptionOptions
            {
                EventTypeIds = new List<string> { eventType.Id }
            });

            Assert.Equal(1, result.Subscribed);

            // List subscriptions
            var list = await _client!.Subscriptions.ListAsync(_workspaceId!, endpoint.Id);
            Assert.Contains(list.Data, s => s.EventTypeId == eventType.Id);

            // Unsubscribe (uses event type public_id in path, returns 204)
            await _client!.Subscriptions.DeleteAsync(_workspaceId!, endpoint.Id, eventType.Id);

            // Verify unsubscribed
            var afterDelete = await _client!.Subscriptions.ListAsync(_workspaceId!, endpoint.Id);
            Assert.DoesNotContain(afterDelete.Data, s => s.EventTypeId == eventType.Id);
        }
        finally
        {
            // Cleanup supporting resources
            await _client!.Endpoints.DeleteAsync(_workspaceId!, endpoint.Id);
            await _client!.EventTypes.DeleteAsync(_workspaceId!, eventType.Id);
        }
    }

    // ──────────────────────────────────────────────
    // Environments CRUD
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "ManagementIntegration")]
    public async Task Environments_Crud()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var uid = Uid();

        // Create first environment
        var created = await _client!.Environments.CreateAsync(_workspaceId!, new CreateEnvironmentOptions
        {
            Name = $"Test Env {uid}",
            Slug = $"test-env-{uid}"
        });

        Assert.False(string.IsNullOrEmpty(created.Id));
        Assert.Equal($"Test Env {uid}", created.Name);
        Assert.Equal($"test-env-{uid}", created.Slug);

        // Create second environment for list assertion
        var second = await _client!.Environments.CreateAsync(_workspaceId!, new CreateEnvironmentOptions
        {
            Name = $"Test Env 2 {uid}",
            Slug = $"test-env-2-{uid}"
        });

        try
        {
            // List (should contain at least 2: the ones we created + possibly default)
            var list = await _client!.Environments.ListAsync(_workspaceId!);
            Assert.True(list.Data.Count >= 2);
            Assert.Contains(list.Data, e => e.Id == created.Id);
            Assert.Contains(list.Data, e => e.Id == second.Id);

            // Get
            var fetched = await _client!.Environments.GetAsync(_workspaceId!, created.Id);
            Assert.Equal(created.Id, fetched.Id);
            Assert.Equal($"Test Env {uid}", fetched.Name);
            Assert.Equal($"test-env-{uid}", fetched.Slug);

            // Update
            var updatedName = $"Updated Env {uid}";
            var updated = await _client!.Environments.UpdateAsync(_workspaceId!, created.Id, new UpdateEnvironmentOptions
            {
                Name = updatedName
            });
            Assert.Equal(updatedName, updated.Name);
        }
        finally
        {
            // Delete (cleanup)
            await _client!.Environments.DeleteAsync(_workspaceId!, created.Id);
            await _client!.Environments.DeleteAsync(_workspaceId!, second.Id);
        }

        // Verify deletion
        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            _client!.Environments.GetAsync(_workspaceId!, created.Id));
        Assert.Equal(404, ex.Status);
    }

    // ──────────────────────────────────────────────
    // EventType Visibility Lifecycle
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "ManagementIntegration")]
    public async Task EventTypeVisibility_Lifecycle()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var uid = Uid();

        // Create supporting resources: environment + event type
        var env = await _client!.Environments.CreateAsync(_workspaceId!, new CreateEnvironmentOptions
        {
            Name = $"Vis Env {uid}",
            Slug = $"vis-env-{uid}"
        });

        var eventType = await _client!.EventTypes.CreateAsync(_workspaceId!, new CreateEventTypeOptions
        {
            Name = $"vis.test.{uid}",
            Description = $"Visibility test event type {uid}"
        });

        try
        {
            // List visibility for the environment
            var list = await _client!.Environments.ListEventTypeVisibilityAsync(_workspaceId!, env.Id);
            Assert.NotNull(list.Data);

            // Set published = true
            var visibility = await _client!.Environments.SetEventTypeVisibilityAsync(
                _workspaceId!, env.Id, eventType.Id, new SetVisibilityOptions { Published = true });

            Assert.Equal(eventType.Id, visibility.EventTypeId);
            Assert.True(visibility.Published);

            // Verify in list
            var afterPublish = await _client!.Environments.ListEventTypeVisibilityAsync(_workspaceId!, env.Id);
            Assert.Contains(afterPublish.Data, v => v.EventTypeId == eventType.Id && v.Published);

            // Set published = false
            var unpublished = await _client!.Environments.SetEventTypeVisibilityAsync(
                _workspaceId!, env.Id, eventType.Id, new SetVisibilityOptions { Published = false });

            Assert.False(unpublished.Published);
        }
        finally
        {
            // Cleanup
            await _client!.Environments.DeleteAsync(_workspaceId!, env.Id);
            await _client!.EventTypes.DeleteAsync(_workspaceId!, eventType.Id);
        }
    }

    // ──────────────────────────────────────────────
    // Deliveries — reads against pre-seeded fixture rows
    // Fixtures live in packages/db/src/seeds/test-fixtures.sql:
    //   del_fixture_001 — delivered, hasPayload=true
    //   del_fixture_002 — failed, 3 attempts, hasPayload=false
    //   del_fixture_003 — delivering, hasPayload=false
    // All scoped to ep_integration_test_001.
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ManagementIntegration")]
    public async Task Deliveries_List_ReturnsSeededDeliveriesWithOpaqueNextCursor()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var result = await _client!.Deliveries.ListAsync(_workspaceId!, "ep_integration_test_001", new ListDeliveriesOptions
        {
            Limit = 2
        });

        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, d => d.Id == "del_fixture_003");
        // With 3 fixture rows and limit=2 we expect a non-null opaque cursor.
        Assert.NotNull(result.NextCursor);
        Assert.False(string.IsNullOrEmpty(result.NextCursor));
        // Cursor must not leak the publicId format.
        Assert.False(result.NextCursor!.StartsWith("del_", StringComparison.Ordinal));
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ManagementIntegration")]
    public async Task Deliveries_List_WithStatusFilter_ReturnsExactlyOneFailedFixture()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var result = await _client!.Deliveries.ListAsync(_workspaceId!, "ep_integration_test_001", new ListDeliveriesOptions
        {
            Status = "failed"
        });

        Assert.Single(result.Data);
        var failed = result.Data[0];
        Assert.Equal("del_fixture_002", failed.Id);
        Assert.Equal("failed", failed.Status);
        Assert.Equal(3, failed.TotalAttempts);
        Assert.False(failed.HasPayload);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ManagementIntegration")]
    public async Task Deliveries_Get_ReturnsMetadataWithoutEnvelopeByDefault()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var delivery = await _client!.Deliveries.GetAsync(_workspaceId!, "del_fixture_001");

        Assert.Equal("del_fixture_001", delivery.Id);
        Assert.Equal("ep_integration_test_001", delivery.EndpointId);
        Assert.Equal("delivered", delivery.Status);
        Assert.True(delivery.HasPayload);
        Assert.Null(delivery.Payload);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ManagementIntegration")]
    public async Task Deliveries_Get_WithIncludePayload_ReturnsEnvelope()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var delivery = await _client!.Deliveries.GetAsync(_workspaceId!, "del_fixture_001", new GetDeliveryOptions
        {
            IncludePayload = true
        });

        Assert.NotNull(delivery.Payload);
        // R2 wiring in the test infra may not be configured, in which case the
        // envelope reports "error" or "not_found". All 5 statuses are valid.
        var validStatuses = new[] { "available", "forbidden", "processing", "not_found", "error" };
        Assert.Contains(delivery.Payload!.Status, validStatuses);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ManagementIntegration")]
    public async Task Deliveries_GetAttempts_ReturnsThreeFixtureAttemptsInChronologicalOrder()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var attempts = await _client!.Deliveries.GetAttemptsAsync(_workspaceId!, "del_fixture_002");

        Assert.Equal(3, attempts.Count);
        Assert.Equal(1, attempts[0].AttemptNumber);
        Assert.Equal(2, attempts[1].AttemptNumber);
        Assert.Equal(3, attempts[2].AttemptNumber);
        Assert.Equal(502, attempts[0].ResponseStatusCode);
    }

    [SkippableFact]
    [Trait("Category", "Integration")]
    [Trait("Category", "ManagementIntegration")]
    public async Task Deliveries_Get_Returns404ForNonExistentDelivery()
    {
        Skip.If(_skip, "Management integration env vars not set");

        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            _client!.Deliveries.GetAsync(_workspaceId!, "del_does_not_exist_anywhere"));

        Assert.Equal(404, ex.Status);
    }

    // ──────────────────────────────────────────────
    // Error Cases
    // ──────────────────────────────────────────────

    [SkippableFact]
    [Trait("Category", "ManagementIntegration")]
    public async Task InvalidToken_Returns401()
    {
        Skip.If(_skip, "Management integration env vars not set");

        using var badClient = new NahookManagement("nhm_invalidtoken_zzzz0000", new NahookManagementOptions
        {
            BaseUrl = _apiUrl
        });

        var ex = await Assert.ThrowsAsync<NahookApiException>(() =>
            badClient.EventTypes.ListAsync(_workspaceId!));

        Assert.Equal(401, ex.Status);
        Assert.True(ex.IsAuthError);
    }
}
