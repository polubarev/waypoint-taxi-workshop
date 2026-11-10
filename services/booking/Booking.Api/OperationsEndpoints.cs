using System.Globalization;
using System.Text;
using System.Text.Json;
using Booking.Domain;
using Npgsql;
using RabbitMQ.Client;
using Shared.Contracts;
using Shared.Infrastructure;

namespace Booking.Api;

public sealed record OperationsSummary(
    Guid BookingId,
    string Team,
    string Region,
    string Status,
    double DistanceKm,
    int AuditEntries,
    DateTimeOffset RequestedAt);

public sealed record ApprovalBatch(Guid[] BookingIds);

public sealed record BatchResult(
    Guid BookingId,
    bool Approved,
    string? Explanation);

public static class OperationsEndpoints
{
    public static void MapOperations(this WebApplication app)
    {
        app.MapGet("/operations/export", Export);

        app.MapGet("/operations/dispatch-summary", DispatchSummary)
            .RequireAuthorization(p => p.RequireRole(Roles.Dispatcher));

        app.MapPost("/operations/approve-batch", ApproveBatch)
            .RequireAuthorization(p => p.RequireRole(Roles.Manager));
    }

    private static async Task<IResult> Export(
        Guid employeeId,
        BookingStore bookings)
    {
        var records = await bookings.Mine(employeeId);
        var csv = new StringBuilder();
        csv.AppendLine("booking_id,team,region,status,distance_km,note,created_at");

        foreach (var booking in records)
        {
            csv.Append(booking.Id);
            csv.Append(',');
            csv.Append(Csv(booking.TeamId));
            csv.Append(',');
            csv.Append(Csv(booking.RegionId));
            csv.Append(',');
            csv.Append(booking.Status);
            csv.Append(',');
            csv.Append(booking.DistanceKm.ToString("0.000", CultureInfo.InvariantCulture));
            csv.Append(',');
            csv.Append(Csv(booking.Note));
            csv.Append(',');
            csv.AppendLine(booking.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return Results.File(bytes, "text/csv", $"trips-{employeeId}.csv");
    }

    public static IReadOnlyList<OperationsSummary> Summarize(
        IEnumerable<BookingDto> bookings)
    {
        return bookings.Select(booking => new OperationsSummary(
            booking.Id,
            booking.TeamId,
            booking.RegionId,
            booking.Status.ToString(),
            booking.DistanceKm,
            0,
            booking.CreatedAt)).ToArray();
    }

    private static async Task<IResult> DispatchSummary(
        HttpContext context,
        BookingStore bookings,
        Database db,
        AuthClient auth,
        PermissionCache permissions)
    {
        var token = context.Request.Headers.Authorization.ToString();
        var actor = context.User.UserId();
        var roles = await permissions.RolesFor(actor,
            async () => (await auth.User(null, token)).Roles);
        Hosting.Require(roles.Contains(Roles.Dispatcher));

        var records = await bookings.Dispatch();
        var summaries = new List<OperationsSummary>(records.Count);

        foreach (var booking in records)
        {
            var counts = await db.Query(
                "SELECT count(*) FROM booking_audit WHERE booking_id=$1",
                reader => reader.GetInt64(0),
                booking.Id);

            summaries.Add(new OperationsSummary(
                booking.Id,
                booking.TeamId,
                booking.RegionId,
                booking.Status.ToString(),
                booking.DistanceKm,
                checked((int)counts.Single()),
                booking.CreatedAt));
        }

        return Results.Ok(summaries);
    }

    private static async Task<IResult> ApproveBatch(
        ApprovalBatch batch,
        HttpContext context,
        BookingStore bookings,
        Database db,
        AuthClient auth,
        PermissionCache permissions,
        IConfiguration configuration)
    {
        if (batch.BookingIds is null || batch.BookingIds.Length is < 1 or > 50)
            throw new ApiError(400, "Select between one and fifty bookings");

        var token = context.Request.Headers.Authorization.ToString();
        var actor = context.User.UserId();
        var roles = await permissions.RolesFor(actor,
            async () => (await auth.User(null, token)).Roles);
        Hosting.Require(roles.Contains(Roles.Manager));

        var results = new List<BatchResult>();
        var factory = new ConnectionFactory
        {
            Uri = new Uri(configuration["Broker:Uri"]!)
        };
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(true, true));

        foreach (var id in batch.BookingIds.Distinct())
        {
            var booking = await bookings.Get(id);
            var employee = await auth.User(booking.EmployeeId, token);
            Hosting.Require(BookingRules.CanApprove(actor, roles, employee.ManagerId));

            if (!BookingRules.CanTransition(booking.Status, BookingStatus.Approved))
            {
                results.Add(new(id, false, "Booking is no longer pending"));
                continue;
            }

            var changed = await db.Execute(
                "UPDATE bookings SET status='Approved' WHERE id=$1 AND status='PendingApproval'",
                id);
            if (changed == 0)
            {
                results.Add(new(id, false, "Booking changed during approval"));
                continue;
            }

            await db.Execute(
                "INSERT INTO booking_audit(booking_id,actor_id,action) VALUES($1,$2,'Approved')",
                id, actor);

            var envelope = EventEnvelope.Create(
                EventTypes.BookingApproved,
                new BookingApproved(id, booking.Pickup, booking.Dropoff, booking.RegionId));
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                MessageId = envelope.EventId.ToString()
            };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, Wire.Json);
            await channel.BasicPublishAsync(
                configuration["Broker:Exchange"] ?? "taxi.events",
                EventTypes.BookingApproved,
                true,
                properties,
                bytes);

            results.Add(new(id, true, null));
        }

        return Results.Ok(results);
    }

    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0]))
            value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
