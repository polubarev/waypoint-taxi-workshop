using System.Text.Json;
using Shared.Contracts;
namespace Gis.Domain;
public static class GeometryRules
{
    public static string? ValidatePolygon(JsonElement geometry)
    {
        if(geometry.ValueKind!=JsonValueKind.Object || !geometry.TryGetProperty("type",out var type) || type.GetString()!="Polygon" || !geometry.TryGetProperty("coordinates",out var rings) || rings.ValueKind!=JsonValueKind.Array || rings.GetArrayLength()==0)
            return "A GeoJSON Polygon with at least one ring is required";
        foreach(var ring in rings.EnumerateArray())
        {
            if(ring.ValueKind!=JsonValueKind.Array || ring.GetArrayLength()<4) return "Each ring needs at least four positions";
            foreach(var position in ring.EnumerateArray())
            {
                if(position.ValueKind!=JsonValueKind.Array || position.GetArrayLength()!=2 || position[0].ValueKind!=JsonValueKind.Number || position[1].ValueKind!=JsonValueKind.Number || !position[0].TryGetDouble(out var lon) || !position[1].TryGetDouble(out var lat) || !new GeoPoint(lat,lon).IsValid)
                    return "Positions must be [longitude, latitude] in WGS84";
            }
            if(ring[0][0].GetDouble()!=ring[ring.GetArrayLength()-1][0].GetDouble() || ring[0][1].GetDouble()!=ring[ring.GetArrayLength()-1][1].GetDouble()) return "Polygon rings must be closed";
        }
        return null;
    }
    public static bool Allowed(bool pickupService,bool dropoffService,bool pickupRestricted,bool dropoffRestricted) => pickupService && dropoffService && !pickupRestricted && !dropoffRestricted;
}
public static class PlacePreview
{
    public static double[] Position(GeoPoint point) => [point.Lat,point.Lng];
}
