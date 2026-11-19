using MongoDB.Driver;
using NC.Application.RPC.Changes;
using NC.Storage.Mongo;
using NoTiming.Application.Contracts.Shared;

namespace NoTiming.EventBus.Outbox;

/// <summary>
/// Sends the changes that documents have marked for announcement. Each document is marked as emitted in the same
/// atomic step that reads it, before it is sent. A write that lands while a notice is being sent sets the marker
/// again, so the next tick announces it.
/// </summary>
internal sealed class OutboxPoller<TDocument, TEvent> : BackgroundService
    where TDocument : class, IOutbox<TEvent>
    where TEvent : ChangeNotice
{
    const string EMITTED = "Emitted";
    static readonly TimeSpan INTERVAL = TimeSpan.FromSeconds(1);

    readonly string _collection;
    readonly string _database;
    readonly ILogger<OutboxPoller<TDocument, TEvent>> _logger;
    readonly IMongoContext _mongo;
    readonly Func<int, TEvent, Task> _publish;

    public OutboxPoller(
        IMongoContext mongo,
        string database,
        string collection,
        Func<int, TEvent, Task> publish,
        ILogger<OutboxPoller<TDocument, TEvent>> logger
    )
    {
        _mongo = mongo;
        _database = database;
        _collection = collection;
        _publish = publish;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(INTERVAL);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SendPending(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Outbox poll of '{Collection}' failed. It will retry on the next tick.", _collection);
            }
        }
    }

    async Task SendPending(CancellationToken cancellationToken)
    {
        var collection = _mongo.Client.GetDatabase(_database).GetCollection<TDocument>(_collection);
        var filter = Builders<TDocument>.Filter.Eq(EMITTED, false);
        var update = Builders<TDocument>.Update.Set(EMITTED, true);
        var options = new FindOneAndUpdateOptions<TDocument>
        {
            ReturnDocument = ReturnDocument.Before,
            Sort = Builders<TDocument>.Sort.Ascending("_id"),
        };

        while (true)
        {
            var document = await collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
            if (document == null)
            {
                return;
            }

            await _publish(document.EventId, document.ToEvent());
        }
    }
}
