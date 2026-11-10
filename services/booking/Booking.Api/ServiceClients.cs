using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shared.Contracts;
using Shared.Infrastructure;
namespace Booking.Api;
public sealed class GisClient(HttpClient client)
{
    public async Task<TripValidationResponse> Validate(TripValidationRequest request,string token,CancellationToken ct=default)
    {
        using var message=new HttpRequestMessage(HttpMethod.Post,"validate") { Content=JsonContent.Create(request,options:Wire.Json) };
        message.Headers.Authorization=AuthenticationHeaderValue.Parse(token);
        try
        {
            using var response=await client.SendAsync(message,ct);
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new ApiError((int)response.StatusCode,"GIS rejected the user's credentials");
            if(!response.IsSuccessStatusCode) throw new ApiError(502,"GIS validation failed");
            return await response.Content.ReadFromJsonAsync<TripValidationResponse>(Wire.Json,ct)??throw new ApiError(502,"GIS returned no validation result");
        }
        catch(Exception error) when(error is HttpRequestException or TaskCanceledException) { throw new ApiError(503,"GIS is unavailable; booking was not created"); }
    }
}
public sealed class AuthClient(HttpClient client)
{
    public async Task<UserInfo> User(Guid? id,string token,CancellationToken ct=default)
    {
        using var message=new HttpRequestMessage(HttpMethod.Get,id.HasValue?$"users/{id}":"me");
        message.Headers.Authorization=AuthenticationHeaderValue.Parse(token);
        try
        {
            using var response=await client.SendAsync(message,ct);
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new ApiError((int)response.StatusCode,"Auth rejected this lookup");
            if(!response.IsSuccessStatusCode) throw new ApiError(502,"Auth user lookup failed");
            return await response.Content.ReadFromJsonAsync<UserInfo>(Wire.Json,ct)??throw new ApiError(502,"Auth returned no user");
        }
        catch(Exception error) when(error is HttpRequestException or TaskCanceledException) { throw new ApiError(503,"Auth is unavailable; please retry"); }
    }
}
