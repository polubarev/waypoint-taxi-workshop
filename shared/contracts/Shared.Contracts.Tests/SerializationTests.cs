using System.Text.Json;
using Shared.Contracts;
public class SerializationTests
{
    [Theory][InlineData(32.06,34.77,true)][InlineData(91,34,false)][InlineData(32,181,false)][InlineData(-90,-180,true)]
    public void CoordinatesValidate(double lat,double lng,bool valid)=>Assert.Equal(valid,new GeoPoint(lat,lng).IsValid);
    [Fact] public void GeoJsonIsLongitudeFirst()=>Assert.Equal(new[]{34.77,32.06},new GeoPoint(32.06,34.77).Coordinates);
    [Fact] public void NonFiniteCoordinatesFail(){Assert.False(new GeoPoint(double.NaN,0).IsValid);Assert.False(new GeoPoint(0,double.PositiveInfinity).IsValid);}
    [Fact] public void EventRoundTripPreservesRouteAndType()
    {
        var payload=new BookingApproved(Guid.NewGuid(),new(32.06,34.77),new(32.09,34.81),"central");
        var envelope=EventEnvelope.Create(EventTypes.BookingApproved,payload);
        var json=JsonSerializer.Serialize(envelope,Wire.Json);
        var read=JsonSerializer.Deserialize<EventEnvelope>(json,Wire.Json)!;
        Assert.Equal(envelope.EventId,read.EventId);Assert.Equal(payload,read.Read<BookingApproved>());Assert.Contains("\"lat\"",json);
    }
    [Fact] public void BookingStatusIsAStableString()=>Assert.Equal("\"PendingApproval\"",JsonSerializer.Serialize(BookingStatus.PendingApproval,Wire.Json));
    [Fact] public void UserClaimsDoNotDrift(){Assert.Equal("roles",Claims.Role);Assert.Equal("managerId",Claims.Manager);Assert.Equal("teamId",Claims.Team);}
}
