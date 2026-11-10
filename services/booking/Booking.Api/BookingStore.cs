using System.Text.Json;
using Booking.Domain;
using Npgsql;
using Shared.Contracts;
using Shared.Infrastructure;
namespace Booking.Api;
public sealed class BookingStore(Database db)
{
    private const string Columns="id,employee_id,manager_id,team_id,pickup::text,dropoff::text,distance_km,region_id,status,taxi_id,note,created_at";
    private static BookingDto Read(NpgsqlDataReader r)=>new(r.GetGuid(0),r.GetGuid(1),r.GetGuid(2),r.GetString(3),JsonSerializer.Deserialize<GeoPoint>(r.GetString(4),Wire.Json)!,JsonSerializer.Deserialize<GeoPoint>(r.GetString(5),Wire.Json)!,r.GetDouble(6),r.GetString(7),Enum.Parse<BookingStatus>(r.GetString(8)),r.IsDBNull(9)?null:r.GetString(9),r.GetString(10),r.GetFieldValue<DateTimeOffset>(11));
    public async Task<BookingDto> Get(Guid id)=>(await db.Query($"SELECT {Columns} FROM bookings WHERE id=$1",Read,id)).SingleOrDefault()??throw new ApiError(404,"Booking not found");
    public Task<List<BookingDto>> Mine(Guid id)=>db.Query($"SELECT {Columns} FROM bookings WHERE employee_id=$1 ORDER BY created_at DESC",Read,id);
    public Task<List<BookingDto>> Team(Guid id)=>db.Query($"SELECT {Columns} FROM bookings WHERE manager_id=$1 ORDER BY created_at DESC",Read,id);
    public Task<List<BookingDto>> Dispatch()=>db.Query($"SELECT {Columns} FROM dispatcher_queue ORDER BY created_at",Read);
    public async Task<BookingDto> Create(UserInfo user,CreateBookingRequest request,TripValidationResponse validation)
    {
        if(!user.ManagerId.HasValue) throw new ApiError(400,"A manager must be assigned before booking");
        if((request.Note?.Length??0)>500) throw new ApiError(400,"Notes must be at most 500 characters");
        var booking=new BookingDto(Guid.NewGuid(),user.Id,user.ManagerId.Value,user.TeamId,request.Pickup,request.Dropoff,validation.DistanceKm,validation.RegionId!,BookingStatus.PendingApproval,null,request.Note??"",DateTimeOffset.UtcNow);
        await using var conn=await db.Source.OpenConnectionAsync();
        await using var tx=await conn.BeginTransactionAsync();
        await using var cmd=new NpgsqlCommand("INSERT INTO bookings(id,employee_id,manager_id,team_id,pickup,dropoff,distance_km,region_id,status,note,created_at) VALUES($1,$2,$3,$4,$5::jsonb,$6::jsonb,$7,$8,$9,$10,$11)",conn,tx);
        Database.Add(cmd,booking.Id,booking.EmployeeId,booking.ManagerId,booking.TeamId,JsonSerializer.Serialize(booking.Pickup,Wire.Json),JsonSerializer.Serialize(booking.Dropoff,Wire.Json),booking.DistanceKm,booking.RegionId,booking.Status.ToString(),booking.Note,booking.CreatedAt);
        await cmd.ExecuteNonQueryAsync();
        await using var audit=new NpgsqlCommand("INSERT INTO booking_audit(booking_id,actor_id,action) VALUES($1,$2,'Created')",conn,tx);
        Database.Add(audit,booking.Id,booking.EmployeeId);
        await audit.ExecuteNonQueryAsync();
        await tx.CommitAsync();
        return booking;
    }
    public async Task<BookingDto> Change(Guid id,Guid actor,BookingStatus next,string? taxi=null)
    {
        await using var conn=await db.Source.OpenConnectionAsync();
        await using var tx=await conn.BeginTransactionAsync();
        BookingDto booking;
        await using(var read=new NpgsqlCommand($"SELECT {Columns} FROM bookings WHERE id=$1 FOR UPDATE",conn,tx))
        {
            Database.Add(read,id);
            await using var reader=await read.ExecuteReaderAsync();
            if(!await reader.ReadAsync()) throw new ApiError(404,"Booking not found");
            booking=Read(reader);
        }
        if(!BookingRules.CanTransition(booking.Status,next)) throw new ApiError(409,$"Cannot change {booking.Status} to {next}");
        if(next==BookingStatus.Assigned)
        {
            await using var check=new NpgsqlCommand("SELECT count(*) FROM taxis WHERE id=$1",conn,tx);
            Database.Add(check,taxi??"");
            if((long)(await check.ExecuteScalarAsync())! != 1) throw new ApiError(400,"Unknown taxi");
        }
        await using(var cmd=new NpgsqlCommand("UPDATE bookings SET status=$2,taxi_id=COALESCE($3,taxi_id) WHERE id=$1",conn,tx))
        {
            Database.Add(cmd,id,next.ToString(),(object?)taxi??DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        await using(var audit=new NpgsqlCommand("INSERT INTO booking_audit(booking_id,actor_id,action) VALUES($1,$2,$3)",conn,tx))
        {
            Database.Add(audit,id,actor,next.ToString());
            await audit.ExecuteNonQueryAsync();
        }
        if(next==BookingStatus.Approved) await Database.Outbox(conn,tx,EventEnvelope.Create(EventTypes.BookingApproved,new BookingApproved(id,booking.Pickup,booking.Dropoff,booking.RegionId)));
        if(next is BookingStatus.Cancelled or BookingStatus.Completed) await Database.Outbox(conn,tx,EventEnvelope.Create(EventTypes.BookingCancelled,new BookingCancelled(id)));
        await tx.CommitAsync();
        return booking with { Status=next,TaxiId=taxi??booking.TaxiId };
    }
    public Task<List<TaxiDto>> Taxis()=>db.Query("SELECT id,driver,plate FROM taxis ORDER BY id",r=>new TaxiDto(r.GetString(0),r.GetString(1),r.GetString(2)));
    public Task<List<AuditDto>> Audit(Guid id)=>db.Query("SELECT booking_id,actor_id,action,at FROM booking_audit WHERE booking_id=$1 ORDER BY id",r=>new AuditDto(r.GetGuid(0),r.GetGuid(1),r.GetString(2),r.GetFieldValue<DateTimeOffset>(3)),id);
    public async Task Seed()
    {
        foreach(var status in Enum.GetValues<BookingStatus>())
        {
            var id=Guid.Parse($"20000000-0000-0000-0000-{(int)status+1:000000000000}");
            var p=new GeoPoint(32.06,34.77); var d=new GeoPoint(32.09,34.81);
            await using var conn=await db.Source.OpenConnectionAsync();
            await using var tx=await conn.BeginTransactionAsync();
            await using var cmd=new NpgsqlCommand("INSERT INTO bookings(id,employee_id,manager_id,team_id,pickup,dropoff,distance_km,region_id,status,taxi_id,note) VALUES($1,$2,$3,'central',$4::jsonb,$5::jsonb,5.04,'central',$6,$7,$8) ON CONFLICT DO NOTHING",conn,tx);
            Database.Add(cmd,id,Guid.Parse("00000000-0000-0000-0000-000000000001"),Guid.Parse("00000000-0000-0000-0000-000000000002"),JsonSerializer.Serialize(p,Wire.Json),JsonSerializer.Serialize(d,Wire.Json),status.ToString(),status is BookingStatus.Assigned or BookingStatus.Completed?(object)"taxi-01":DBNull.Value,"Sample: "+status);
            var inserted=await cmd.ExecuteNonQueryAsync();
            if(inserted>0)
            {
                await using var audit=new NpgsqlCommand("INSERT INTO booking_audit(booking_id,actor_id,action) VALUES($1,$2,'Seeded')",conn,tx);
                Database.Add(audit,id,Guid.Parse("00000000-0000-0000-0000-000000000005"));
                await audit.ExecuteNonQueryAsync();
                if(status is BookingStatus.Approved or BookingStatus.Assigned) await Database.Outbox(conn,tx,EventEnvelope.Create(EventTypes.BookingApproved,new BookingApproved(id,p,d,"central")));
            }
            await tx.CommitAsync();
        }
    }
}
