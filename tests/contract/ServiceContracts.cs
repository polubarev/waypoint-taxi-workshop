using System.Net;
using System.Net.Http.Json;
using Booking.Api;
using Shared.Contracts;
using Shared.Infrastructure;
public class ServiceContracts
{
    private sealed class Stub(Func<HttpRequestMessage,Task<HttpResponseMessage>> action):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>action(request);
    }
    [Fact] public async Task GisConsumerForwardsUserTokenAndLongitudeLatitude()
    {
        var pickup=new GeoPoint(32.06,34.77);var dropoff=new GeoPoint(32.09,34.81);
        var handler=new Stub(async request=>
        {
            Assert.Equal("http://gis:8080/validate",request.RequestUri!.ToString());Assert.Equal("Bearer employee-jwt",request.Headers.Authorization!.ToString());
            var body=await request.Content!.ReadFromJsonAsync<TripValidationRequest>(Wire.Json);Assert.Equal(pickup,body!.Pickup);Assert.Equal(dropoff,body.Dropoff);
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new TripValidationResponse(true,null,5.04,"central"),options:Wire.Json)};
        });
        var consumer=new GisClient(new HttpClient(handler){BaseAddress=new("http://gis:8080/")});
        var result=await consumer.Validate(new(pickup,dropoff),"Bearer employee-jwt");Assert.True(result.Allowed);Assert.Equal(5.04,result.DistanceKm);Assert.Equal("central",result.RegionId);
    }
    [Theory][InlineData(401,401)][InlineData(403,403)][InlineData(500,502)]
    public async Task DownstreamGisFailureCannotBecomeAnAllowedBooking(int responseCode,int expected)
    {
        var client=new GisClient(new HttpClient(new Stub(_=>Task.FromResult(new HttpResponseMessage((HttpStatusCode)responseCode)))){BaseAddress=new("http://gis/")});
        var error=await Assert.ThrowsAsync<ApiError>(()=>client.Validate(new(new(32,34),new(33,35)),"Bearer token"));Assert.Equal(expected,error.Status);
    }
    [Fact] public async Task AuthLookupForwardsManagerTokenAndUsesEmployeeId()
    {
        var id=Guid.NewGuid();var manager=Guid.NewGuid();
        var handler=new Stub(request=>
        {
            Assert.Equal($"http://auth/users/{id}",request.RequestUri!.ToString());Assert.Equal("Bearer manager-jwt",request.Headers.Authorization!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new UserInfo(id,"employee","Employee","central",manager,[Roles.Employee]),options:Wire.Json)});
        });
        var user=await new AuthClient(new HttpClient(handler){BaseAddress=new("http://auth/")}).User(id,"Bearer manager-jwt");Assert.Equal(manager,user.ManagerId);
    }
    [Fact] public async Task GisOutageReturns503()
    {
        var client=new GisClient(new HttpClient(new Stub(_=>throw new HttpRequestException("offline"))){BaseAddress=new("http://gis/")});
        var error=await Assert.ThrowsAsync<ApiError>(()=>client.Validate(new(new(32,34),new(33,35)),"Bearer token"));Assert.Equal(503,error.Status);
    }
}
