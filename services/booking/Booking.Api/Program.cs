using System.Security.Claims;
using Booking.Api;
using Booking.Domain;
using Shared.Contracts;
using Shared.Infrastructure;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddTaxiAuth(builder.Configuration);
builder.Services.AddSingleton(new Database(builder.Configuration.GetConnectionString("Database")!));
builder.Services.AddSingleton<BookingStore>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<PermissionCache>();
builder.Services.AddSingleton<IEventHandler>(s=>s.GetRequiredService<PermissionCache>());
builder.Services.AddSingleton<BrokerState>();
builder.Services.AddHostedService<EventWorker>();
builder.Services.AddHttpClient<GisClient>(c=>{c.BaseAddress=new Uri(builder.Configuration["Services:GisUrl"]!);c.Timeout=TimeSpan.FromSeconds(5);});
builder.Services.AddHttpClient<AuthClient>(c=>{c.BaseAddress=new Uri(builder.Configuration["Services:AuthUrl"]!);c.Timeout=TimeSpan.FromSeconds(5);});
var app=builder.Build();
app.UseTaxiErrors();
await app.Services.GetRequiredService<Database>().Initialize(typeof(BookingStore).Assembly);
await app.Services.GetRequiredService<BookingStore>().Seed();
app.MapGet("/health",()=>new{service="booking",status="ok"});
app.MapGet("/ready",(BrokerState state)=>state.Connected?Results.Ok(new{status="ready"}):Results.StatusCode(503));
var api=app.MapGroup("").RequireAuthorization().AddEndpointFilter<ContractFilter>();
static string Token(HttpContext c)=>c.Request.Headers.Authorization.ToString();
static async Task<string[]> CurrentRoles(HttpContext c,AuthClient auth,PermissionCache cache)=>await cache.RolesFor(c.User.UserId(),async()=>(await auth.User(null,Token(c))).Roles);
api.MapPost("/bookings",async(CreateBookingRequest request,HttpContext context,GisClient gis,AuthClient auth,PermissionCache cache,BookingStore store)=>
{
    var user=await auth.User(null,Token(context));
    Hosting.Require((await CurrentRoles(context,auth,cache)).Contains(Roles.Employee));
    var validation=await gis.Validate(new(request.Pickup,request.Dropoff),Token(context));
    if(!validation.Allowed) throw new ApiError(422,validation.Reason??"Trip is not allowed");
    var booking=await store.Create(user,request,validation);
    return Results.Created($"/booking/bookings/{booking.Id}",booking);
});
api.MapGet("/bookings",(ClaimsPrincipal actor,BookingStore store)=>store.Mine(actor.UserId()));
api.MapGet("/team/bookings",async(HttpContext context,AuthClient auth,PermissionCache cache,BookingStore store)=>
{
    Hosting.Require((await CurrentRoles(context,auth,cache)).Contains(Roles.Manager));
    return await store.Team(context.User.UserId());
});
foreach(var decision in new[]{("approve",BookingStatus.Approved),("reject",BookingStatus.Rejected)})
{
    var next=decision.Item2;
    api.MapPost("/bookings/{id:guid}/"+decision.Item1,async(Guid id,HttpContext context,AuthClient auth,PermissionCache cache,BookingStore store)=>
    {
        var booking=await store.Get(id);
        var employee=await auth.User(booking.EmployeeId,Token(context));
        Hosting.Require(BookingRules.CanApprove(context.User.UserId(),await CurrentRoles(context,auth,cache),employee.ManagerId),"Only the employee's manager may approve or reject");
        return await store.Change(id,context.User.UserId(),next);
    });
}
api.MapPost("/bookings/{id:guid}/cancel",async(Guid id,HttpContext context,AuthClient auth,PermissionCache cache,BookingStore store)=>
{
    var booking=await store.Get(id);
    Hosting.Require(BookingRules.CanCancel(context.User.UserId(),await CurrentRoles(context,auth,cache),booking));
    return await store.Change(id,context.User.UserId(),BookingStatus.Cancelled);
});
api.MapGet("/bookings/{id:guid}/audit",async(Guid id,HttpContext context,AuthClient auth,PermissionCache cache,BookingStore store)=>
{
    var booking=await store.Get(id);var actor=context.User.UserId();var roles=await CurrentRoles(context,auth,cache);
    Hosting.Require(actor==booking.EmployeeId||(actor==booking.ManagerId&&roles.Contains(Roles.Manager))||roles.Contains(Roles.Dispatcher));
    return await store.Audit(id);
});
api.MapGet("/dispatch/approved",async(HttpContext context,AuthClient auth,PermissionCache cache,BookingStore store)=>
{
    Hosting.Require((await CurrentRoles(context,auth,cache)).Contains(Roles.Dispatcher));return await store.Dispatch();
});
api.MapGet("/dispatch/taxis",async(HttpContext context,AuthClient auth,PermissionCache cache,BookingStore store)=>
{
    Hosting.Require((await CurrentRoles(context,auth,cache)).Contains(Roles.Dispatcher));return await store.Taxis();
});
api.MapPost("/dispatch/{id:guid}/assign",async(Guid id,AssignTaxiRequest request,HttpContext context,AuthClient auth,PermissionCache cache,BookingStore store)=>
{
    Hosting.Require((await CurrentRoles(context,auth,cache)).Contains(Roles.Dispatcher));return await store.Change(id,context.User.UserId(),BookingStatus.Assigned,request.TaxiId);
});
api.MapPost("/dispatch/{id:guid}/complete",async(Guid id,HttpContext context,AuthClient auth,PermissionCache cache,BookingStore store)=>
{
    Hosting.Require((await CurrentRoles(context,auth,cache)).Contains(Roles.Dispatcher));return await store.Change(id,context.User.UserId(),BookingStatus.Completed);
});
app.Run();
