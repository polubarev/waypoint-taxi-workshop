using System.Text.Json;
using Gis.Domain;
using Npgsql;
using Shared.Contracts;
using Shared.Infrastructure;
namespace Gis.Api;
public sealed class GisStore(Database db)
{
    public async Task<object> Zones()
    {
        var rows=await db.Query("SELECT id,name,type,active,region_id,ST_AsGeoJSON(geom) FROM zones ORDER BY name",r=>new
        {
            type="Feature",id=r.GetGuid(0),properties=new { name=r.GetString(1),type=r.GetString(2),active=r.GetBoolean(3),regionId=r.GetString(4) },geometry=JsonSerializer.Deserialize<JsonElement>(r.GetString(5))
        });
        return new {type="FeatureCollection",features=rows};
    }
    public async Task<Guid> Save(Guid? id,ZoneRequest request)
    {
        var error=GeometryRules.ValidatePolygon(request.Geometry);
        if(error is not null) throw new ApiError(400,error);
        if(string.IsNullOrWhiteSpace(request.Name)||request.Name.Length>100||string.IsNullOrWhiteSpace(request.RegionId)||request.RegionId.Length>50||!Enum.IsDefined(request.Type)) throw new ApiError(400,"Valid name, region, and zone type are required");
        var geometry=request.Geometry.GetRawText();
        var validity=await db.Query("SELECT ST_IsValid(g),ST_IsValidReason(g) FROM (SELECT ST_SetSRID(ST_GeomFromGeoJSON($1),4326) g) q",r=>(Valid:r.GetBoolean(0),Reason:r.GetString(1)),geometry);
        if(!validity[0].Valid) throw new ApiError(400,"Invalid polygon: "+validity[0].Reason);
        var zoneId=id??Guid.NewGuid();
        if(id.HasValue)
        {
            var affected=await db.Execute("UPDATE zones SET name=$2,type=$3,active=$4,region_id=$5,geom=ST_SetSRID(ST_GeomFromGeoJSON($6),4326) WHERE id=$1",zoneId,request.Name,request.Type.ToString(),request.Active,request.RegionId,geometry);
            if(affected==0) throw new ApiError(404,"Zone not found");
        }
        else await db.Execute("INSERT INTO zones VALUES($1,$2,$3,$4,$5,ST_SetSRID(ST_GeomFromGeoJSON($6),4326))",zoneId,request.Name,request.Type.ToString(),request.Active,request.RegionId,geometry);
        return zoneId;
    }
    public async Task<TripValidationResponse> Validate(TripValidationRequest request)
    {
        if(!request.Pickup.IsValid || !request.Dropoff.IsValid) throw new ApiError(400,"Coordinates must be finite WGS84 positions");
        const string sql="""
            WITH points AS (SELECT ST_SetSRID(ST_MakePoint($1,$2),4326) p,ST_SetSRID(ST_MakePoint($3,$4),4326) d),
            active AS (SELECT * FROM zones WHERE active AND ST_IsValid(geom))
            SELECT EXISTS(SELECT 1 FROM active,points WHERE type='Service' AND ST_Covers(geom,p)),
                   EXISTS(SELECT 1 FROM active,points WHERE type='Service' AND ST_Covers(geom,d)),
                   EXISTS(SELECT 1 FROM active,points WHERE type='Restricted' AND ST_Covers(geom,p)),
                   EXISTS(SELECT 1 FROM active,points WHERE type='Restricted' AND ST_Covers(geom,d)),
                   ST_Distance(ST_Transform(p,2039),ST_Transform(d,2039))/1000.0,
                   (SELECT region_id FROM active WHERE type='Service' AND ST_Covers(geom,p) ORDER BY region_id LIMIT 1)
            FROM points
            """;
        var rows=await db.Query(sql,r=>
        {
            var inZone=r.GetBoolean(0)&&r.GetBoolean(1);
            var restricted=r.GetBoolean(2)||r.GetBoolean(3);
            return new TripValidationResponse(inZone&&!restricted,!inZone?"Both points must be inside an active service zone":restricted?"A point is inside a restricted zone":null,Math.Round(r.GetDouble(4),3),r.IsDBNull(5)?null:r.GetString(5),inZone,restricted);
        },request.Pickup.Lng,request.Pickup.Lat,request.Dropoff.Lng,request.Dropoff.Lat);
        return rows.Single();
    }
    public async Task<object> LiveTrips()
    {
        var rows=await db.Query("SELECT row_id,booking_id,pickup::text,dropoff::text,region_id FROM live_trips ORDER BY row_id",r=>
        {
            var p=JsonSerializer.Deserialize<GeoPoint>(r.GetString(2),Wire.Json)!;
            var d=JsonSerializer.Deserialize<GeoPoint>(r.GetString(3),Wire.Json)!;
            return new {type="Feature",id=r.GetInt64(0),properties=new { bookingId=r.GetGuid(1),regionId=r.GetString(4) },geometry=new { type="LineString",coordinates=new[]{p.Coordinates,d.Coordinates} }};
        });
        return new {type="FeatureCollection",features=rows};
    }
}
public sealed class TripLayerConsumer(Database db) : IEventHandler
{
    public async Task Handle(EventEnvelope envelope)
    {
        switch(envelope.Type)
        {
            case EventTypes.BookingApproved:
                var trip=envelope.Read<BookingApproved>();
                await db.Execute("INSERT INTO live_trips(booking_id,event_id,pickup,dropoff,region_id,occurred_at) SELECT $1,$2,$3::jsonb,$4::jsonb,$5,$6 WHERE NOT EXISTS(SELECT 1 FROM closed_trips WHERE booking_id=$1)",trip.BookingId,envelope.EventId,JsonSerializer.Serialize(trip.Pickup,Wire.Json),JsonSerializer.Serialize(trip.Dropoff,Wire.Json),trip.RegionId,envelope.OccurredAt);
                break;
            case EventTypes.BookingCancelled:
                var cancelled=envelope.Read<BookingCancelled>();
                await using(var conn=await db.Source.OpenConnectionAsync())
                await using(var tx=await conn.BeginTransactionAsync())
                {
                    await using var cmd=new NpgsqlCommand("INSERT INTO closed_trips VALUES($1,$2) ON CONFLICT DO NOTHING",conn,tx);
                    Database.Add(cmd,cancelled.BookingId,envelope.OccurredAt);
                    await cmd.ExecuteNonQueryAsync();
                    await using var remove=new NpgsqlCommand("DELETE FROM live_trips WHERE booking_id=$1",conn,tx);
                    Database.Add(remove,cancelled.BookingId);
                    await remove.ExecuteNonQueryAsync();
                    await tx.CommitAsync();
                }
                break;
            default: throw new JsonException("Unsupported trip event");
        }
    }
}
