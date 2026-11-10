using Microsoft.AspNetCore.Http;
using Shared.Contracts;
namespace Shared.Infrastructure;

public sealed class ContractFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach(var argument in context.Arguments)
        {
            var problem=argument switch
            {
                LoginRequest r when string.IsNullOrWhiteSpace(r.Username)||string.IsNullOrEmpty(r.Password)||r.Username.Length>80||r.Password.Length>256 => "Username and password are required",
                RefreshRequest r when string.IsNullOrWhiteSpace(r.RefreshToken)||r.RefreshToken.Length>200 => "Refresh token is required",
                CreateUserRequest r when string.IsNullOrWhiteSpace(r.Username)||string.IsNullOrWhiteSpace(r.DisplayName)||string.IsNullOrWhiteSpace(r.Password)||r.Password.Length>256||string.IsNullOrWhiteSpace(r.TeamId)||r.Roles is null => "All user fields and roles are required",
                ChangeRolesRequest r when r.Roles is null => "Roles are required",
                TripValidationRequest r when r.Pickup is null||r.Dropoff is null => "Pickup and drop-off are required",
                CreateBookingRequest r when r.Pickup is null||r.Dropoff is null => "Pickup and drop-off are required",
                AssignTaxiRequest r when string.IsNullOrWhiteSpace(r.TaxiId) => "Taxi ID is required",
                ZoneRequest r when string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.RegionId) => "Zone name and region are required",
                _ => null
            };
            if(problem is not null) throw new ApiError(400,problem);
        }
        return next(context);
    }
}
