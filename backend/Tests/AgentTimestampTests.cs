using System.Text.Json;
using Application.Mappings;
using Domain.Entities;
using Domain.Enums;

namespace Tests;

public class AgentTimestampTests
{
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void Response_serializes_UTC_without_changing_entity_or_instant(DateTimeKind kind)
    {
        var timestamp = new DateTime(2026, 10, 20, 23, 23, 39, kind).AddTicks(7999673);
        var entity = new Agent { LocationUpdatedAt = timestamp, Role = AgentRole.TrafficAgent };
        var response = entity.ToResponse();
        var expectedTicks = kind == DateTimeKind.Local ? timestamp.ToUniversalTime().Ticks : timestamp.Ticks;
        Assert.Equal(DateTimeKind.Utc, response.LocationUpdatedAt.Kind);
        Assert.Equal(expectedTicks, response.LocationUpdatedAt.Ticks);
        Assert.Equal(kind, entity.LocationUpdatedAt.Kind);
        Assert.Equal(timestamp.Ticks, entity.LocationUpdatedAt.Ticks);
        Assert.Equal(AgentRole.TrafficAgent, response.Role);
        var json = JsonSerializer.SerializeToElement(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.EndsWith("Z", json.GetProperty("locationUpdatedAt").GetString());
        if (kind != DateTimeKind.Local)
            Assert.Equal("2026-10-20T23:23:39.7999673Z", json.GetProperty("locationUpdatedAt").GetString());
    }
}
