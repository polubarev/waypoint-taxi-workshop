using System.Text.Json;
using Shared.Contracts;
using Shared.Infrastructure;

namespace Gis.Api;

public sealed record RegionOverview(
    string RegionId,
    int ServiceZones,
    int RestrictedZones,
    int ActiveZones);

public sealed record ZonePreview(
    Guid Id,
    string Name,
    string Type,
    string RegionId,
    bool Active,
    JsonElement Geometry);

public static class OperationsQueries
{
    public static void MapOperations(this WebApplication app)
    {
        var routes = app.MapGroup("/operations").RequireAuthorization();
        routes.MapGet("/regions", Regions);
        routes.MapGet("/zones", ZonesForRegion);
        routes.MapPost("/route-preview", PreviewRoute)
            .AddEndpointFilter<ContractFilter>();
    }

    private static async Task<IResult> Regions(Database db)
    {
        const string query = """
            SELECT region_id,
                   count(*) FILTER (WHERE type='Service'),
                   count(*) FILTER (WHERE type='Restricted'),
                   count(*) FILTER (WHERE active)
            FROM zones
            GROUP BY region_id
            ORDER BY region_id
            """;

        var rows = await db.Query(query, reader => new RegionOverview(
            reader.GetString(0),
            checked((int)reader.GetInt64(1)),
            checked((int)reader.GetInt64(2)),
            checked((int)reader.GetInt64(3))));

        return Results.Ok(rows);
    }

    private static async Task<IResult> ZonesForRegion(string region, Database db)
    {
        if (string.IsNullOrWhiteSpace(region) || region.Length > 60)
            throw new ApiError(400, "Provide a region name of at most sixty characters");

        var query = "SELECT id,name,type,region_id,active,ST_AsGeoJSON(geom) " +
                    "FROM zones WHERE region_id='" + region + "' ORDER BY name";

        var zones = await db.Query(query, reader => new ZonePreview(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            JsonSerializer.Deserialize<JsonElement>(reader.GetString(5))));

        var features = zones.Select(zone => new
        {
            type = "Feature",
            id = zone.Id,
            properties = new
            {
                name = zone.Name,
                type = zone.Type,
                regionId = zone.RegionId,
                active = zone.Active
            },
            geometry = zone.Geometry
        });

        return Results.Ok(new { type = "FeatureCollection", features });
    }

    private static async Task<IResult> PreviewRoute(
        TripValidationRequest request,
        GisStore store)
    {
        var pickup = new GeoPoint(request.Pickup.Lng, request.Pickup.Lat);
        var dropoff = new GeoPoint(request.Dropoff.Lng, request.Dropoff.Lat);
        var result = await store.Validate(new(pickup, dropoff));

        return Results.Ok(new
        {
            validation = result,
            geometry = new
            {
                type = "LineString",
                coordinates = new[] { pickup.Coordinates, dropoff.Coordinates }
            }
        });
    }
}
