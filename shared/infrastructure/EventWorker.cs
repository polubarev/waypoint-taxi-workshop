using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.Contracts;
namespace Shared.Infrastructure;

public interface IEventHandler { Task Handle(EventEnvelope envelope); }
public sealed class BrokerState { public bool Connected { get; set; } }
public sealed class EventWorker(Database db, IConfiguration config, IEventHandler handler, BrokerState state, ILogger<EventWorker> log) : BackgroundService
{
    private static readonly (string Suffix, string[] Types)[] Subscriptions =
    [
        ("gis.live", [EventTypes.BookingApproved, EventTypes.BookingCancelled]),
        ("auth.audit", [EventTypes.BookingApproved]),
        ("booking.permissions", [EventTypes.UserRoleChanged])
    ];
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = new ConnectionFactory { Uri = new Uri(config["Broker:Uri"]!), AutomaticRecoveryEnabled = true };
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true,true),stoppingToken);
                var prefix = config["Broker:QueuePrefix"] ?? "taxi";
                var queue = prefix + "." + config["Broker:ConsumerSuffix"];
                var exchange = config["Broker:Exchange"] ?? "taxi.events";
                await channel.ExchangeDeclareAsync(exchange,ExchangeType.Topic,true,false,cancellationToken:stoppingToken);
                foreach (var sub in Subscriptions)
                {
                    var name = prefix + "." + sub.Suffix;
                    await channel.QueueDeclareAsync(name,true,false,false,cancellationToken:stoppingToken);
                    await channel.QueueDeclareAsync(name+".dead",true,false,false,cancellationToken:stoppingToken);
                    await channel.QueueDeclareAsync(name+".retry",true,false,false,new Dictionary<string,object?>
                    {
                        ["x-message-ttl"] = config.GetValue("Broker:RetryDelayMs",1500),
                        ["x-dead-letter-exchange"] = "", ["x-dead-letter-routing-key"] = name
                    },cancellationToken:stoppingToken);
                    foreach (var type in sub.Types)
                        await channel.QueueBindAsync(name,exchange,type,cancellationToken:stoppingToken);
                }
                state.Connected = true;
                log.LogInformation("Broker connected: exchange={Exchange}, consumer={Queue}",exchange,queue);
                var ackBatchSize = config.GetValue("Broker:AckBatchSize",50);
                var ackInterval = TimeSpan.FromMilliseconds(config.GetValue("Broker:AckIntervalMs",300000));
                ulong lastTag = 0; var pending = 0; var firstPending = DateTimeOffset.UtcNow;
                while (!stoppingToken.IsCancellationRequested)
                {
                    await PublishOutbox(channel,exchange,stoppingToken);
                    if (pending > 0 && (pending >= ackBatchSize || DateTimeOffset.UtcNow - firstPending >= ackInterval))
                    {
                        await channel.BasicAckAsync(lastTag,true,stoppingToken);
                        pending = 0;
                    }
                    var message = await channel.BasicGetAsync(queue,false,stoppingToken);
                    if (message is null) { await Task.Delay(150,stoppingToken); continue; }
                    try
                    {
                        var envelope = JsonSerializer.Deserialize<EventEnvelope>(message.Body.Span,Wire.Json) ?? throw new JsonException("Empty envelope");
                        await handler.Handle(envelope);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        var count = message.BasicProperties.Headers?.TryGetValue("x-attempt",out var value) == true ? Convert.ToInt32(value) : 0;
                        var destination = queue + (count >= config.GetValue("Broker:MaxRetries",3) ? ".dead" : ".retry");
                        var properties = new BasicProperties { Persistent=true, ContentType="application/json", Headers=new Dictionary<string,object?> { ["x-attempt"]=count+1 } };
                        await channel.BasicPublishAsync("",destination,true,properties,message.Body,stoppingToken);
                        log.LogWarning(error,"Event failed; sent to {Destination}, attempt {Attempt}",destination,count+1);
                    }
                    if (pending++ == 0) firstPending = DateTimeOffset.UtcNow;
                    lastTag = message.DeliveryTag;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                state.Connected = false;
                log.LogWarning(error,"Broker worker reconnecting; unpublished outbox rows retained");
                await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken);
            }
            finally { state.Connected = false; }
        }
    }
    private async Task PublishOutbox(IChannel channel,string exchange,CancellationToken ct)
    {
        var events = await db.Query("SELECT id,type,payload::text FROM outbox WHERE published_at IS NULL ORDER BY created_at LIMIT 20",r=>(Id:r.GetGuid(0),Type:r.GetString(1),Payload:r.GetString(2)));
        foreach (var item in events)
        {
            var properties = new BasicProperties { Persistent=true,ContentType="application/json",MessageId=item.Id.ToString(),Type=item.Type };
            await channel.BasicPublishAsync(exchange,item.Type,true,properties,System.Text.Encoding.UTF8.GetBytes(item.Payload),ct);
            await db.Execute("UPDATE outbox SET published_at=now() WHERE id=$1",item.Id);
        }
    }
}
