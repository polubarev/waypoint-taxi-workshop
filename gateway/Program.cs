using Shared.Contracts;
using Shared.Infrastructure;
var builder=WebApplication.CreateBuilder(args);
builder.Services.AddTaxiAuth(builder.Configuration);
builder.Services.AddAuthorization(o=>
{
    o.AddPolicy("authenticated",p=>p.RequireAuthenticatedUser());
    o.AddPolicy("manager",p=>p.RequireRole(Roles.Manager));
    o.AddPolicy("dispatcher",p=>p.RequireRole(Roles.Dispatcher));
    o.AddPolicy("system-admin",p=>p.RequireRole(Roles.SystemAdmin));
});
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
var app=builder.Build();
app.UseTaxiErrors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/health",()=>new{service="gateway",status="ok"});
app.MapReverseProxy();
app.Run();
