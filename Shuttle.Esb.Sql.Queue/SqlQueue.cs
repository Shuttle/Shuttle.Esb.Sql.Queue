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

    private readonly string _machineName;
    private readonly IScriptProvider _scriptProvider;

    private readonly SqlQueueOptions _sqlQueueOptions;
    private readonly byte[] _unacknowledgedHash;

    private IQuery? _countQuery;
    private IQuery? _createQuery;
    private IQuery? _dropQuery;
    private IQuery? _existsQuery;
    private IQuery? _purgeQuery;
    private string _dequeueIdQueryStatement = string.Empty;
    private string _enqueueQueryStatement = string.Empty;
    private string _removeQueryStatement = string.Empty;

    private bool _initialized;
    
    public SqlQueue(QueueUri uri, SqlQueueOptions sqlQueueOptions, IScriptProvider scriptProvider, IDatabaseContextFactory databaseContextFactory, CancellationToken cancellationToken)
    {
        Uri = Guard.AgainstNull(uri);

        _sqlQueueOptions = Guard.AgainstNull(sqlQueueOptions);
        _scriptProvider = Guard.AgainstNull(scriptProvider);
        _databaseContextFactory = Guard.AgainstNull(databaseContextFactory);

        _cancellationToken = cancellationToken;

        _machineName = Environment.MachineName;
        _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _unacknowledgedHash = MD5.Create().ComputeHash(Encoding.ASCII.GetBytes($@"{_machineName}\\{_baseDirectory}"));
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
        if (Guard.AgainstNull(acknowledgementToken) is not (int sequenceId and > 0))
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
                var query = new Query(_removeQueryStatement)
                    .AddParameter(QueueColumns.SequenceId, sequenceId);

                await databaseContext.ExecuteAsync(query, _cancellationToken).ConfigureAwait(false);
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
                await databaseContext.ExecuteAsync(_createQuery!, _cancellationToken).ConfigureAwait(false);
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

                await databaseContext.ExecuteAsync(_dropQuery!, _cancellationToken).ConfigureAwait(false);
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
                await databaseContext.ExecuteAsync(
                        new Query(_enqueueQueryStatement)
                            .AddParameter(QueueColumns.MessageId, transportMessage.MessageId)
                            .AddParameter(QueueColumns.MessageBody, await stream.ToBytesAsync().ConfigureAwait(false)), _cancellationToken).ConfigureAwait(false);

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

    private async ValueTask<bool> ExistsAsync(IDatabaseContext databaseContext)
    {
        if (!_initialized)
        {
            Initialize();
        }

        return await databaseContext.GetScalarAsync<int>(_existsQuery!, _cancellationToken).ConfigureAwait(false) == 1;
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
                var query = new Query(_scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueDequeue, Uri.QueueName))
                    .AddParameter(QueueColumns.MachineName, _machineName)
                    .AddParameter(QueueColumns.QueueName, Uri.QueueName)
                    .AddParameter(QueueColumns.UnacknowledgedHash, _unacknowledgedHash)
                    .AddParameter(QueueColumns.UnacknowledgedId, Guid.NewGuid());

                var row = await databaseContext.GetRowAsync(query, _cancellationToken).ConfigureAwait(true);

                if (row == null)
                {
                    return null;
                }

                var result = new MemoryStream((byte[])row["MessageBody"]);

                var receivedMessage = new ReceivedMessage(result, QueueColumns.SequenceId.Value(row));

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

    private void Initialize()
    {
        Operation?.Invoke(this, new("[initialize/starting]"));

        _createQuery = new Query(_scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueCreate, Uri.QueueName))
            .AddParameter(QueueColumns.UnacknowledgedHash, _unacknowledgedHash)
            .AddParameter(QueueColumns.MachineName, _machineName)
            .AddParameter(QueueColumns.QueueName, Uri.QueueName)
            .AddParameter(QueueColumns.BaseDirectory, _baseDirectory);

        _existsQuery = new Query(_scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueExists, Uri.QueueName));
        _dropQuery = new Query(_scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueDrop, Uri.QueueName));
        _purgeQuery = new Query(_scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueuePurge, Uri.QueueName));
        _countQuery = new Query(_scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueCount, Uri.QueueName));
        _enqueueQueryStatement = _scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueEnqueue, Uri.QueueName);
        _removeQueryStatement = _scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueRemove, Uri.QueueName);
        _dequeueIdQueryStatement = _scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueDequeueId, Uri.QueueName);

        using (new DatabaseContextScope())
        using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
        {
            databaseContext.ExecuteAsync(new Query(_scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueRelease, Uri.QueueName))
                .AddParameter(QueueColumns.UnacknowledgedHash, _unacknowledgedHash), cancellationToken: _cancellationToken).GetAwaiter().GetResult();
        }

        _initialized = true;

        Operation?.Invoke(this, new("[initialize/completed]"));
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
                var result = await databaseContext.GetScalarAsync<int>(_countQuery!, _cancellationToken).ConfigureAwait(false) == 0;

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

                await databaseContext.ExecuteAsync(_purgeQuery!, _cancellationToken).ConfigureAwait(false);
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

    public async Task ReleaseAsync(object acknowledgementToken)
    {
        if (Guard.AgainstNull(acknowledgementToken) is not (int sequenceId and > 0))
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
                var row = await databaseContext.GetRowAsync(
                        new Query(_dequeueIdQueryStatement)
                            .AddParameter(QueueColumns.SequenceId, sequenceId), cancellationToken: _cancellationToken).ConfigureAwait(false);

                if (row != null)
                {
                    await databaseContext.ExecuteAsync(new Query(_removeQueryStatement)
                            .AddParameter(QueueColumns.SequenceId, sequenceId), _cancellationToken).ConfigureAwait(false);

                    await databaseContext.ExecuteAsync(
                        new Query(_enqueueQueryStatement)
                            .AddParameter(QueueColumns.MessageId, QueueColumns.MessageId.Value(row))
                            .AddParameter(QueueColumns.MessageBody, row["MessageBody"]), _cancellationToken).ConfigureAwait(false);

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
}