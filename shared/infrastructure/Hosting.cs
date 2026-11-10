using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shared.Contracts;
namespace Shared.Infrastructure;

public sealed class ApiError(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public static class Hosting
{
    public static IServiceCollection AddTaxiAuth(this IServiceCollection services, IConfiguration config)
    {
        var key = config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required");
        if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("JWT signing key must be at least 32 bytes");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "taxi-auth", ValidateAudience = true, ValidAudience = "taxi-services",
                ValidateLifetime = true, RequireExpirationTime = true, ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                NameClaimType = Claims.Subject, RoleClaimType = Claims.Role, ClockSkew = TimeSpan.FromSeconds(5)
            };
        });
        services.AddAuthorization();
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return services;
    }
    public static void UseTaxiErrors(this WebApplication app)
    {
        app.Use(async (context,next) =>
        {
            try { await next(context); }
            catch (ApiError e) { context.Response.StatusCode=e.Status; await context.Response.WriteAsJsonAsync(new { error=e.Message }); }
            catch (System.Text.Json.JsonException) { context.Response.StatusCode=400; await context.Response.WriteAsJsonAsync(new { error="Invalid JSON payload" }); }
            catch (BadHttpRequestException e) { context.Response.StatusCode=e.StatusCode; await context.Response.WriteAsJsonAsync(new { error="Invalid request body" }); }
        });
        app.UseAuthentication();
        app.UseAuthorization();
    }
    public static Guid UserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(Claims.Subject)!);
    public static string[] UserRoles(this ClaimsPrincipal user) => user.FindAll(Claims.Role).Select(c=>c.Value).ToArray();
    public static void Require(bool allowed, string reason="Permission denied") { if (!allowed) throw new ApiError(403,reason); }
}
