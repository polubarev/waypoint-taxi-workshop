using System.Security.Claims;
using System.Threading.RateLimiting;
using Auth.Api;
using Auth.Domain;
using Microsoft.AspNetCore.RateLimiting;
using Shared.Contracts;
using Shared.Infrastructure;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddTaxiAuth(builder.Configuration);
builder.Services.AddSingleton(new Database(builder.Configuration.GetConnectionString("Database")!));
builder.Services.AddSingleton<AuthStore>();
builder.Services.AddSingleton<Tokens>();
builder.Services.AddSingleton<BrokerState>();
builder.Services.AddSingleton<IEventHandler,LegacyApprovalAudit>();
builder.Services.AddHostedService<EventWorker>();
builder.Services.AddRateLimiter(o=>o.AddPolicy("login",c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions { PermitLimit=60,Window=TimeSpan.FromMinutes(1) })));
var app=builder.Build();
app.UseTaxiErrors();
app.UseRateLimiter();
var db=app.Services.GetRequiredService<Database>();
await db.Initialize(typeof(AuthStore).Assembly);
await app.Services.GetRequiredService<AuthStore>().Seed(builder.Configuration["Seed:Password"] ?? throw new InvalidOperationException("Seed:Password required"));
app.MapGet("/health",()=>Results.Ok(new {service="auth",status="ok"}));
app.MapGet("/ready",(BrokerState state)=>state.Connected?Results.Ok(new{status="ready"}):Results.StatusCode(503));
var api=app.MapGroup("").AddEndpointFilter<ContractFilter>();
api.MapPost("/login",async(LoginRequest request,AuthStore users,Tokens tokens)=>
{
    var user=await users.Login(request.Username,request.Password) ?? throw new ApiError(401,"Invalid username or password");
    return await tokens.Issue(user);
}).RequireRateLimiting("login");
api.MapPost("/refresh",(RefreshRequest request,Tokens tokens)=>tokens.Rotate(request.RefreshToken)).RequireRateLimiting("login");
api.MapPost("/logout",async(RefreshRequest request,Database database)=>{await database.Execute("DELETE FROM refresh_tokens WHERE hash=$1",Passwords.TokenHash(request.RefreshToken));return Results.NoContent();}).RequireAuthorization();
app.MapGet("/me",async(ClaimsPrincipal actor,AuthStore users)=>await users.Find(actor.UserId())??throw new ApiError(404,"User not found")).RequireAuthorization();
app.MapGet("/users/{id:guid}",async(Guid id,ClaimsPrincipal actor,AuthStore users)=>
{
    var target=await users.Find(id)??throw new ApiError(404,"User not found");
    Hosting.Require(UserRules.CanSeeUser(actor.UserId(),actor.UserRoles(),id,target.ManagerId));
    return target;
}).RequireAuthorization();
var admin=api.MapGroup("/admin").RequireAuthorization(p=>p.RequireRole(Roles.SystemAdmin));
admin.MapGet("/roles",(Database database)=>database.Query("SELECT name FROM roles ORDER BY name",r=>r.GetString(0)));
admin.MapGet("/users",(AuthStore users)=>users.All());
admin.MapGet("/teams",(Database database)=>database.Query("SELECT id,name,manager_id FROM teams ORDER BY id",r=>new TeamInfo(r.GetString(0),r.GetString(1),r.GetGuid(2))));
admin.MapPost("/users",async(CreateUserRequest request,AuthStore users)=>{await users.Create(request);return Results.Created("/admin/users",new{message="User created"});});
admin.MapPut("/users/{id:guid}/roles",async(Guid id,ChangeRolesRequest request,AuthStore users)=>{await users.SetRoles(id,request.Roles);return Results.NoContent();});
app.Run();
