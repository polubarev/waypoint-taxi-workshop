using System.Text.Json;
using System.Text.Json.Serialization;
namespace Shared.Contracts;

public static class Roles
{
    public const string Employee = "Employee", Manager = "Manager", Dispatcher = "Dispatcher", GisAdmin = "GisAdmin", SystemAdmin = "SystemAdmin";
    public static readonly string[] All = [Employee, Manager, Dispatcher, GisAdmin, SystemAdmin];
}
public static class Claims
{
    public const string Subject = "sub", Role = "roles", Team = "teamId", Manager = "managerId";
}
public static class EventTypes
{
    public const string BookingApproved = "BookingApproved", BookingCancelled = "BookingCancelled", UserRoleChanged = "UserRoleChanged";
}
public static class Wire
{
    public static readonly JsonSerializerOptions Json = Create();
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
public enum BookingStatus { PendingApproval, Approved, Rejected, Assigned, Completed, Cancelled }
public enum ZoneType { Service, Restricted }
public sealed record GeoPoint(double Lat, double Lng)
{
    [JsonIgnore] public bool IsValid => double.IsFinite(Lat) && double.IsFinite(Lng) && Lat is >= -90 and <= 90 && Lng is >= -180 and <= 180;
    [JsonIgnore] public double[] Coordinates => [Lng, Lat];
}
public sealed record TripValidationRequest(GeoPoint Pickup, GeoPoint Dropoff);
public sealed record TripValidationResponse(bool Allowed, string? Reason, double DistanceKm, string? RegionId, bool InZone = true, bool Blocked = false);
public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, UserInfo User);
public sealed record UserInfo(Guid Id, string Username, string DisplayName, string TeamId, Guid? ManagerId, string[] Roles);
public sealed record CreateUserRequest(string Username, string DisplayName, string Password, string TeamId, Guid? ManagerId, string[] Roles);
public sealed record ChangeRolesRequest(string[] Roles);
public sealed record TeamInfo(string Id, string Name, Guid ManagerId);
public sealed record CreateBookingRequest(GeoPoint Pickup, GeoPoint Dropoff, string? Note);
public sealed record AssignTaxiRequest(string TaxiId);
public sealed record BookingDto(Guid Id, Guid EmployeeId, Guid ManagerId, string TeamId, GeoPoint Pickup, GeoPoint Dropoff, double DistanceKm, string RegionId, BookingStatus Status, string? TaxiId, string Note, DateTimeOffset CreatedAt);
public sealed record AuditDto(Guid BookingId, Guid ActorId, string Action, DateTimeOffset At);
public sealed record TaxiDto(string Id, string Driver, string Plate);
public sealed record ZoneRequest(string Name, ZoneType Type, bool Active, string RegionId, JsonElement Geometry);
public sealed record BookingApproved(Guid BookingId, GeoPoint Pickup, GeoPoint Dropoff, string RegionId);
public sealed record BookingCancelled(Guid BookingId);
public sealed record UserRoleChanged(Guid UserId, string[] Roles, long Version);
public sealed record EventEnvelope(Guid EventId, string Type, DateTimeOffset OccurredAt, JsonElement Data)
{
    public static EventEnvelope Create<T>(string type, T data) => new(Guid.NewGuid(), type, DateTimeOffset.UtcNow, JsonSerializer.SerializeToElement(data, Wire.Json));
    public T Read<T>() => Data.Deserialize<T>(Wire.Json) ?? throw new JsonException("Missing event data");
}
