using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;
using Shuttle.Core.Streams;

namespace Shuttle.Esb.Sql.Queue;

public class SqlQueue : IQueue, ICreateQueue, IDropQueue, IPurgeQueue
{
    private static readonly SemaphoreSlim Lock = new(1, 1);

    private readonly string _baseDirectory;
    private readonly CancellationToken _cancellationToken;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly IQueryFactory _queryFactory;

    private readonly SqlQueueOptions _sqlQueueOptions;
    private readonly byte[] _unacknowledgedHash;

    private bool _initialized;

    public SqlQueue(QueueUri uri, SqlQueueOptions sqlQueueOptions, IDatabaseContextFactory databaseContextFactory, IQueryFactory queryFactory, CancellationToken cancellationToken)
    {
        Uri = Guard.AgainstNull(uri);

        _sqlQueueOptions = Guard.AgainstNull(sqlQueueOptions);
        _databaseContextFactory = Guard.AgainstNull(databaseContextFactory);
        _queryFactory = Guard.AgainstNull(queryFactory);

        _queryFactory = queryFactory;
        _cancellationToken = cancellationToken;

        _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _unacknowledgedHash = MD5.Create().ComputeHash(Encoding.ASCII.GetBytes($@"{Environment.MachineName}\\{_baseDirectory}"));
    }

    public async Task CreateAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[create/cancelled]"));
            return;
        }

        Operation?.Invoke(this, new("[create/starting]"));

        await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_initialized)
        {
            Initialize();
        }

        try
        {
            using (new DatabaseContextScope())
            await using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                await databaseContext.ExecuteAsync(_queryFactory.Create(_sqlQueueOptions.Schema, Uri.QueueName), _cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Operation?.Invoke(this, new("[create/cancelled]"));
        }
        finally
        {
            Lock.Release();
        }

        Operation?.Invoke(this, new("[create/completed]"));
    }

    public async Task DropAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[drop/cancelled]"));
            return;
        }

        Operation?.Invoke(this, new("[drop/starting]"));

        await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_initialized)
        {
            Initialize();
        }

        try
        {
            using (new DatabaseContextScope())
            await using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                if (!await ExistsAsync(databaseContext).ConfigureAwait(false))
                {
                    return;
                }

                await databaseContext.ExecuteAsync(_queryFactory.Drop(_sqlQueueOptions.Schema, Uri.QueueName), _cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Operation?.Invoke(this, new("[drop/cancelled]"));
        }
        finally
        {
            Lock.Release();
        }

        Operation?.Invoke(this, new("[drop/completed]"));
    }

    public async Task PurgeAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[purge/cancelled]"));
            return;
        }

        Operation?.Invoke(this, new("[purge/starting]"));

        await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_initialized)
        {
            Initialize();
        }

        try
        {
            using (new DatabaseContextScope())
            await using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                if (!await ExistsAsync(databaseContext).ConfigureAwait(false))
                {
                    return;
                }

                await databaseContext.ExecuteAsync(_queryFactory.Purge(_sqlQueueOptions.Schema, Uri.QueueName), _cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Operation?.Invoke(this, new("[purge/cancelled]"));
        }
        finally
        {
            Lock.Release();
        }

        Operation?.Invoke(this, new("[purge/completed]"));
    }

    public QueueUri Uri { get; }
    public bool IsStream => false;

    public event EventHandler<MessageEnqueuedEventArgs>? MessageEnqueued;
    public event EventHandler<MessageAcknowledgedEventArgs>? MessageAcknowledged;
    public event EventHandler<MessageReleasedEventArgs>? MessageReleased;
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<OperationEventArgs>? Operation;

    public async Task AcknowledgeAsync(object acknowledgementToken)
    {
        if (Guard.AgainstNull(acknowledgementToken) is not (long sequenceId and > 0))
        {
            return;
        }

        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[acknowledge/cancelled]"));
            return;
        }

        await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_initialized)
        {
            Initialize();
        }

        try
        {
            using (new DatabaseContextScope())
            await using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                await databaseContext.ExecuteAsync(_queryFactory.Acknowledge(_sqlQueueOptions.Schema, Uri.QueueName, sequenceId), _cancellationToken).ConfigureAwait(false);
            }

            MessageAcknowledged?.Invoke(this, new(acknowledgementToken));
        }
        catch (OperationCanceledException)
        {
            Operation?.Invoke(this, new("[acknowledge/cancelled]"));
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task EnqueueAsync(TransportMessage transportMessage, Stream stream)
    {
        Guard.AgainstNull(transportMessage);
        Guard.AgainstNull(stream);

        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[enqueue/cancelled]"));
            return;
        }

        await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_initialized)
        {
            Initialize();
        }

        try
        {
            using (new DatabaseContextScope())
            await using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                await databaseContext.ExecuteAsync(_queryFactory.Enqueue(_sqlQueueOptions.Schema, Uri.QueueName, transportMessage.MessageId, await stream.ToBytesAsync()), _cancellationToken);

                MessageEnqueued?.Invoke(this, new(transportMessage, stream));
            }
        }
        catch (OperationCanceledException)
        {
            Operation?.Invoke(this, new("[enqueue/cancelled]"));
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task<ReceivedMessage?> GetMessageAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[get-message/cancelled]"));
            return null;
        }

        await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_initialized)
        {
            Initialize();
        }

        try
        {
            using (new DatabaseContextScope())
            await using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                var row = await databaseContext.GetRowAsync(_queryFactory.GetMessage(_sqlQueueOptions.Schema, Uri.QueueName, _unacknowledgedHash), _cancellationToken).ConfigureAwait(true);

                if (row == null)
                {
                    return null;
                }

                var result = new MemoryStream((byte[])row["MessageBody"]);

                var receivedMessage = new ReceivedMessage(result, Columns.SequenceId.Value(row));

                if (receivedMessage != null)
                {
                    MessageReceived?.Invoke(this, new(receivedMessage));
                }

                return receivedMessage;
            }
        }
        catch (OperationCanceledException)
        {
            Operation?.Invoke(this, new("[get-message/cancelled]"));
        }
        finally
        {
            Lock.Release();
        }

        return null;
    }

    public async ValueTask<bool> IsEmptyAsync()
    {
        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[is-empty/cancelled]", true));
            return true;
        }

        Operation?.Invoke(this, new("[is-empty/starting]"));

        await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_initialized)
        {
            Initialize();
        }

        try
        {
            using (new DatabaseContextScope())
            await using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                var result = await databaseContext.GetScalarAsync<int>(_queryFactory.Count(_sqlQueueOptions.Schema, Uri.QueueName), _cancellationToken).ConfigureAwait(false) == 0;

                Operation?.Invoke(this, new("[is-empty]", result));

                return result;
            }
        }
        catch (OperationCanceledException)
        {
            Operation?.Invoke(this, new("[is-empty/cancelled]", true));
        }
        finally
        {
            Lock.Release();
        }

        return true;
    }

    public async Task ReleaseAsync(object acknowledgementToken)
    {
        if (Guard.AgainstNull(acknowledgementToken) is not (long sequenceId and > 0))
        {
            return;
        }

        if (_cancellationToken.IsCancellationRequested)
        {
            Operation?.Invoke(this, new("[release/cancelled]"));
            return;
        }

        await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        if (!_initialized)
        {
            Initialize();
        }

        try
        {
            using (new DatabaseContextScope())
            await using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            await using (var transaction = await databaseContext.BeginTransactionAsync().ConfigureAwait(false))
            {
                var row = await databaseContext.GetRowAsync(_queryFactory.Dequeue(_sqlQueueOptions.Schema, Uri.QueueName, sequenceId), _cancellationToken).ConfigureAwait(false);

                if (row != null)
                {
                    await databaseContext.ExecuteAsync(_queryFactory.Remove(_sqlQueueOptions.Schema, Uri.QueueName, sequenceId), _cancellationToken).ConfigureAwait(false);
                    await databaseContext.ExecuteAsync(_queryFactory.Enqueue(_sqlQueueOptions.Schema, Uri.QueueName, Columns.MessageId.Value(row), Guard.AgainstNull(Columns.MessageBody.Value(row), "MessageBody")), _cancellationToken).ConfigureAwait(false);

                    await transaction.CommitTransactionAsync().ConfigureAwait(false);
                }
            }

            MessageReleased?.Invoke(this, new(acknowledgementToken));
        }
        catch (OperationCanceledException)
        {
            Operation?.Invoke(this, new("[release/cancelled]"));
        }
        finally
        {
            Lock.Release();
        }
    }

    private async ValueTask<bool> ExistsAsync(IDatabaseContext databaseContext)
    {
        if (!_initialized)
        {
            Initialize();
        }

        return await databaseContext.GetScalarAsync<int>(_queryFactory.Exists(_sqlQueueOptions.Schema, Uri.QueueName), _cancellationToken).ConfigureAwait(false) == 1;
    }

    private void Initialize()
    {
        Operation?.Invoke(this, new("[initialize/starting]"));

        using (new DatabaseContextScope())
        using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
        {
            databaseContext.ExecuteAsync(_queryFactory.Release(_sqlQueueOptions.Schema, Uri.QueueName, _unacknowledgedHash), _cancellationToken).GetAwaiter().GetResult();
        }

        _initialized = true;

        Operation?.Invoke(this, new("[initialize/completed]"));
    }
}