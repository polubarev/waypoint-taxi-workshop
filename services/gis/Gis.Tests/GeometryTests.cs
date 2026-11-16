using System.Text.Json;
using Gis.Domain;
using Shared.Contracts;
public class GeometryTests
{
    private static JsonElement Json(string value)=>JsonSerializer.Deserialize<JsonElement>(value);
    [Fact] public void AcceptsLongitudeFirstClosedPolygon()=>Assert.Null(GeometryRules.ValidatePolygon(Json("""{"type":"Polygon","coordinates":[[[34.7,32.0],[34.8,32.0],[34.8,32.1],[34.7,32.0]]]}""")));
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"type\":\"Point\",\"coordinates\":[34,32]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[34,32],[35,32],[35,33]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[34,32],[35,32],[35,33],[34,33]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[200,32],[35,32],[35,33],[200,32]]]}")]
    [InlineData("{\"type\":\"Polygon\",\"coordinates\":[[[34,92],[35,32],[35,33],[34,92]]]}")]
    public void RejectsMalformedGeometry(string json)=>Assert.NotNull(GeometryRules.ValidatePolygon(Json(json)));
    [Theory][InlineData(true,true,false,false,true)][InlineData(false,true,false,false,false)][InlineData(true,false,false,false,false)][InlineData(true,true,true,false,false)][InlineData(true,true,false,true,false)]
    public void BothPointsMustPass(bool p,bool d,bool pr,bool dr,bool expected)=>Assert.Equal(expected,GeometryRules.Allowed(p,d,pr,dr));
    [Fact] public void TripLineRunsFromPickupToDropoff()=>Assert.Equal(new[]{new[]{32.06,34.77},new[]{32.09,34.81}},TripLayer.Line(new GeoPoint(32.06,34.77),new GeoPoint(32.09,34.81)));
}
