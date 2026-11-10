using Auth.Domain;
using Npgsql;
using Shared.Contracts;
using Shared.Infrastructure;
namespace Auth.Api;
public sealed class AuthStore(Database db)
{
    public static UserInfo Read(NpgsqlDataReader r) => new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.IsDBNull(4)?null:r.GetGuid(4),r.GetFieldValue<string[]>(5));
    private const string Columns="id,username,display_name,team_id,manager_id,roles";
    public async Task<UserInfo?> Find(Guid id) => (await db.Query($"SELECT {Columns} FROM users WHERE id=$1",Read,id)).SingleOrDefault();
    public Task<List<UserInfo>> All() => db.Query($"SELECT {Columns} FROM users ORDER BY username",Read);
    public async Task<UserInfo?> Login(string name,string password)
    {
        var rows = await db.Query($"SELECT {Columns},password_hash FROM users WHERE username=$1",r=>(User:Read(r),Hash:r.GetString(6)),name.ToLowerInvariant());
        var row = rows.SingleOrDefault();
        return row.User is not null && Passwords.Verify(password,row.Hash) ? row.User : null;
    }
    public async Task Create(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length>80 || string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Length>120 || request.Password.Length<10 || !UserRules.ValidRoles(request.Roles))
            throw new ApiError(400,"Username, display name, password of at least 10 characters, and valid unique roles are required");
        var teams=await db.Query("SELECT manager_id FROM teams WHERE id=$1",r=>r.GetGuid(0),request.TeamId);
        if(teams.Count==0 || (request.ManagerId.HasValue && request.ManagerId!=teams[0])) throw new ApiError(400,"Manager must manage the selected team");
        try { await db.Execute("INSERT INTO users(id,username,display_name,password_hash,team_id,manager_id,roles) VALUES ($1,$2,$3,$4,$5,$6,$7)",Guid.NewGuid(),request.Username.Trim().ToLowerInvariant(),request.DisplayName.Trim(),Passwords.Hash(request.Password),request.TeamId,(object?)request.ManagerId??DBNull.Value,request.Roles); }
        catch(PostgresException e) when(e.SqlState=="23505") { throw new ApiError(409,"Username already exists"); }
    }
    public async Task SetRoles(Guid id,string[] roles)
    {
        if(!UserRules.ValidRoles(roles)) throw new ApiError(400,"At least one valid unique role is required");
        await using var conn=await db.Source.OpenConnectionAsync();
        await using var tx=await conn.BeginTransactionAsync();
        await using var cmd=new NpgsqlCommand("UPDATE users SET roles=$1,role_version=role_version+1 WHERE id=$2 RETURNING role_version",conn,tx);
        Database.Add(cmd,roles,id);
        var version=await cmd.ExecuteScalarAsync() ?? throw new ApiError(404,"User not found");
        await Database.Outbox(conn,tx,EventEnvelope.Create(EventTypes.UserRoleChanged,new UserRoleChanged(id,roles,(long)version)));
        await tx.CommitAsync();
    }
    public async Task Seed(string password)
    {
        foreach(var role in Roles.All) await db.Execute("INSERT INTO roles VALUES($1) ON CONFLICT DO NOTHING",role);
        var managerA=Guid.Parse("00000000-0000-0000-0000-000000000002");
        var managerB=Guid.Parse("00000000-0000-0000-0000-000000000007");
        await db.Execute("INSERT INTO teams VALUES ('central','Central team',$1),('north','Northern team',$2) ON CONFLICT DO NOTHING",managerA,managerB);
        var entries=new[] {
            (1,"employee","Noa Cohen","central",(Guid?)managerA,Roles.Employee),
            (2,"manager","Amir Levi","central",(Guid?)null,Roles.Manager),
            (3,"dispatcher","Dana Shalev","central",(Guid?)null,Roles.Dispatcher),
            (4,"gisadmin","Maya Rosen","central",(Guid?)null,Roles.GisAdmin),
            (5,"admin","System administrator","central",(Guid?)null,Roles.SystemAdmin),
            (6,"employee-north","Omer Gal","north",(Guid?)managerB,Roles.Employee),
            (7,"manager-north","Lior Bar","north",(Guid?)null,Roles.Manager)
        };
        foreach(var (number,name,display,team,manager,role) in entries)
            await db.Execute("INSERT INTO users(id,username,display_name,password_hash,team_id,manager_id,roles) VALUES ($1,$2,$3,$4,$5,$6,$7) ON CONFLICT DO NOTHING",Guid.Parse($"00000000-0000-0000-0000-{number:000000000000}"),name,display,Passwords.Hash(password),team,(object?)manager??DBNull.Value,new[]{role});
    }
}
public sealed class LegacyApprovalAudit(Database db) : IEventHandler
{
    public async Task Handle(EventEnvelope envelope)
    {
        if(envelope.Type==EventTypes.BookingApproved)
            await db.Execute("INSERT INTO booking_audit(event_id,booking_id) VALUES($1,$2) ON CONFLICT DO NOTHING",envelope.EventId,envelope.Read<BookingApproved>().BookingId);
    }
}
