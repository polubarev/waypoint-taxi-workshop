using System.Reflection;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Shared.Contracts;
namespace Shared.Infrastructure;

public sealed class Database(string connectionString) : IAsyncDisposable
{
    public NpgsqlDataSource Source { get; } = NpgsqlDataSource.Create(connectionString);
    public async Task Initialize(Assembly assembly, CancellationToken ct = default)
    {
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("schema.sql", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        await using var command = Source.CreateCommand(await reader.ReadToEndAsync(ct));
        await command.ExecuteNonQueryAsync(ct);
    }
    public async Task<int> Execute(string sql, params object[] args)
    {
        await using var cmd = Source.CreateCommand(sql);
        Add(cmd, args);
        return await cmd.ExecuteNonQueryAsync();
    }
    public async Task<List<T>> Query<T>(string sql, Func<NpgsqlDataReader,T> map, params object[] args)
    {
        await using var cmd = Source.CreateCommand(sql);
        Add(cmd, args);
        await using var reader = await cmd.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync()) rows.Add(map(reader));
        return rows;
    }
    public static void Add(NpgsqlCommand cmd, params object[] args)
    {
        foreach (var arg in args) cmd.Parameters.AddWithValue(arg);
    }
    public static async Task Outbox(NpgsqlConnection conn, NpgsqlTransaction tx, EventEnvelope envelope)
    {
        await using var cmd = new NpgsqlCommand("INSERT INTO outbox(id,type,payload) VALUES ($1,$2,$3)",conn,tx);
        cmd.Parameters.AddWithValue(envelope.EventId);
        cmd.Parameters.AddWithValue(envelope.Type);
        cmd.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(envelope,Wire.Json));
        await cmd.ExecuteNonQueryAsync();
    }
    public ValueTask DisposeAsync() => Source.DisposeAsync();
}
