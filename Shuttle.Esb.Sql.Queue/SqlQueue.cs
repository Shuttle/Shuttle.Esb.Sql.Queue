using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;
using Shuttle.Core.Streams;

namespace Shuttle.Esb.Sql.Queue
{
    public class SqlQueue : IQueue, ICreateQueue, IDropQueue, IPurgeQueue
    {
        private static readonly SemaphoreSlim Lock = new SemaphoreSlim(1, 1);

        private readonly string _baseDirectory;
        private readonly IDatabaseContextService _databaseContextService;
        private readonly CancellationToken _cancellationToken;
        private readonly IDatabaseContextFactory _databaseContextFactory;
        private readonly IDatabaseGateway _databaseGateway;

        private readonly string _machineName;
        private readonly IScriptProvider _scriptProvider;

        private readonly SqlQueueOptions _sqlQueueOptions;
        private readonly byte[] _unacknowledgedHash;

        private IQuery _countQuery;
        private IQuery _createQuery;
        private string _dequeueIdQueryStatement;
        private IQuery _dropQuery;
        private string _enqueueQueryStatement;
        private IQuery _existsQuery;
        private bool _initialized;
        private IQuery _purgeQuery;

        private string _removeQueryStatement;

        public SqlQueue(QueueUri uri, SqlQueueOptions sqlQueueOptions, IScriptProvider scriptProvider, IDatabaseContextService databaseContextService, IDatabaseContextFactory databaseContextFactory, IDatabaseGateway databaseGateway, CancellationToken cancellationToken)
        {
            Uri = Guard.AgainstNull(uri, nameof(uri));

            _sqlQueueOptions = Guard.AgainstNull(sqlQueueOptions, nameof(sqlQueueOptions));
            _scriptProvider = Guard.AgainstNull(scriptProvider, nameof(scriptProvider));
            _databaseContextService = Guard.AgainstNull(databaseContextService, nameof(databaseContextService));
            _databaseContextFactory = Guard.AgainstNull(databaseContextFactory, nameof(databaseContextFactory));
            _databaseGateway = Guard.AgainstNull(databaseGateway, nameof(databaseGateway));

            _cancellationToken = cancellationToken;

            _machineName = Environment.MachineName;
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _unacknowledgedHash = MD5.Create().ComputeHash(Encoding.ASCII.GetBytes($@"{_machineName}\\{_baseDirectory}"));
        }

        public void Create()
        {
            CreateAsync(true).GetAwaiter().GetResult();
        }

        public async Task CreateAsync()
        {
            await CreateAsync(false).ConfigureAwait(false);
        }

        public void Drop()
        {
            DropAsync(true).GetAwaiter().GetResult();
        }

        public async Task DropAsync()
        {
            await DropAsync(false).ConfigureAwait(false);
        }

        public void Purge()
        {
            PurgeAsync(true).GetAwaiter().GetResult();
        }

        public async Task PurgeAsync()
        {
            await PurgeAsync(false).ConfigureAwait(false);
        }

        public async ValueTask<bool> IsEmptyAsync()
        {
            return await IsEmptyAsync(false).ConfigureAwait(false);
        }

        public void Enqueue(TransportMessage transportMessage, Stream stream)
        {
            EnqueueAsync(transportMessage, stream, true).GetAwaiter().GetResult();
        }

        public async Task EnqueueAsync(TransportMessage transportMessage, Stream stream)
        {
            await EnqueueAsync(transportMessage, stream, false).ConfigureAwait(false);
        }

        public async Task ReleaseAsync(object acknowledgementToken)
        {
            await ReleaseAsync(acknowledgementToken, false).ConfigureAwait(false);
        }

        public QueueUri Uri { get; }
        public bool IsStream => false;
        public event EventHandler<MessageEnqueuedEventArgs> MessageEnqueued;
        public event EventHandler<MessageAcknowledgedEventArgs> MessageAcknowledged;
        public event EventHandler<MessageReleasedEventArgs> MessageReleased;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<OperationEventArgs> Operation;

        public bool IsEmpty()
        {
            return IsEmptyAsync(true).GetAwaiter().GetResult();
        }

        public ReceivedMessage GetMessage()
        {
            return GetMessageAsync(true).GetAwaiter().GetResult();
        }

        public async Task<ReceivedMessage> GetMessageAsync()
        {
            return await GetMessageAsync(false).ConfigureAwait(false);
        }

        public void Acknowledge(object acknowledgementToken)
        {
            AcknowledgeAsync(acknowledgementToken, true).GetAwaiter().GetResult();
        }

        public async Task AcknowledgeAsync(object acknowledgementToken)
        {
            await AcknowledgeAsync(acknowledgementToken, false).ConfigureAwait(false);
        }

        public void Release(object acknowledgementToken)
        {
            ReleaseAsync(acknowledgementToken, true).GetAwaiter().GetResult();
        }

        private async Task AcknowledgeAsync(object acknowledgementToken, bool sync)
        {
            Guard.AgainstNull(acknowledgementToken, nameof(acknowledgementToken));

            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[acknowledge/cancelled]"));
                return;
            }

            await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                var sequenceId = (int)acknowledgementToken;

                if (sequenceId == 0)
                {
                    return;
                }

                using (_databaseContextService.BeginScope())
                await using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                {
                    var query = new Query(_removeQueryStatement)
                        .AddParameter(QueueColumns.SequenceId, sequenceId);

                    if (sync)
                    {
                        _databaseGateway.Execute(query, _cancellationToken);
                    }
                    else
                    {
                        await _databaseGateway.ExecuteAsync(query, _cancellationToken).ConfigureAwait(false);
                    }
                }

                MessageAcknowledged?.Invoke(this, new MessageAcknowledgedEventArgs(acknowledgementToken));
            }
            catch (OperationCanceledException)
            {
                Operation?.Invoke(this, new OperationEventArgs("[acknowledge/cancelled]"));
            }
            finally
            {
                Lock.Release();
            }
        }

        private async Task CreateAsync(bool sync)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[create/cancelled]"));
                return;
            }

            Operation?.Invoke(this, new OperationEventArgs("[create/starting]"));

            await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                using (_databaseContextService.BeginScope())
                await using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                {
                    if (sync)
                    {
                        _databaseGateway.Execute(_createQuery, _cancellationToken);
                    }
                    else
                    {
                        await _databaseGateway.ExecuteAsync(_createQuery, _cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Operation?.Invoke(this, new OperationEventArgs("[create/cancelled]"));
            }
            finally
            {
                Lock.Release();
            }

            Operation?.Invoke(this, new OperationEventArgs("[create/completed]"));
        }

        private async Task DropAsync(bool sync)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[drop/cancelled]"));
                return;
            }

            Operation?.Invoke(this, new OperationEventArgs("[drop/starting]"));

            await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                using (_databaseContextService.BeginScope())
                await using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                {
                    if (sync)
                    {
                        if (!ExistsAsync(true).GetAwaiter().GetResult())
                        {
                            return;
                        }

                        _databaseGateway.Execute(_dropQuery, _cancellationToken);
                    }
                    else
                    {
                        if (!await ExistsAsync(false).ConfigureAwait(false))
                        {
                            return;
                        }

                        await _databaseGateway.ExecuteAsync(_dropQuery, _cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Operation?.Invoke(this, new OperationEventArgs("[drop/cancelled]"));
            }
            finally
            {
                Lock.Release();
            }

            Operation?.Invoke(this, new OperationEventArgs("[drop/completed]"));
        }

        private async Task EnqueueAsync(TransportMessage transportMessage, Stream stream, bool sync)
        {
            Guard.AgainstNull(transportMessage, nameof(transportMessage));
            Guard.AgainstNull(stream, nameof(stream));

            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[enqueue/cancelled]"));
                return;
            }

            await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                using (_databaseContextService.BeginScope())
                await using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                {
                    if (sync)
                    {
                        _databaseGateway.Execute(
                            new Query(_enqueueQueryStatement)
                                .AddParameter(QueueColumns.MessageId, transportMessage.MessageId)
                                .AddParameter(QueueColumns.MessageBody, stream.ToBytes()), _cancellationToken);
                    }
                    else
                    {
                        await _databaseGateway.ExecuteAsync(
                            new Query(_enqueueQueryStatement)
                                .AddParameter(QueueColumns.MessageId, transportMessage.MessageId)
                                .AddParameter(QueueColumns.MessageBody, await stream.ToBytesAsync().ConfigureAwait(false)), _cancellationToken).ConfigureAwait(false);
                    }

                    MessageEnqueued?.Invoke(this, new MessageEnqueuedEventArgs(transportMessage, stream));
                }
            }
            catch (OperationCanceledException)
            {
                Operation?.Invoke(this, new OperationEventArgs("[enqueue/cancelled]"));
            }
            finally
            {
                Lock.Release();
            }
        }

        private async ValueTask<bool> ExistsAsync(bool sync)
        {
            if (!_initialized)
            {
                Initialize();
            }

            return sync
                ? _databaseGateway.GetScalar<int>(_existsQuery, _cancellationToken) == 1
                : await _databaseGateway.GetScalarAsync<int>(_existsQuery, _cancellationToken).ConfigureAwait(false) == 1;
        }

        private async Task<ReceivedMessage> GetMessageAsync(bool sync)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[get-message/cancelled]"));
                return null;
            }

            await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                using (_databaseContextService.BeginScope())
                await using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName).ConfigureAwait(false))
                {
                    var query = new Query(_scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueDequeue, Uri.QueueName))
                        .AddParameter(QueueColumns.MachineName, _machineName)
                        .AddParameter(QueueColumns.QueueName, Uri.QueueName)
                        .AddParameter(QueueColumns.UnacknowledgedHash, _unacknowledgedHash)
                        .AddParameter(QueueColumns.UnacknowledgedId, Guid.NewGuid());

                    var row = sync
                        ? _databaseGateway.GetRow(query, _cancellationToken)
                        : await _databaseGateway.GetRowAsync(query, _cancellationToken).ConfigureAwait(true);

                    if (row == null)
                    {
                        return null;
                    }

                    var result = new MemoryStream((byte[])row["MessageBody"]);

                    var receivedMessage = new ReceivedMessage(result, QueueColumns.SequenceId.Value(row));

                    if (receivedMessage != null)
                    {
                        MessageReceived?.Invoke(this, new MessageReceivedEventArgs(receivedMessage));
                    }

                    return receivedMessage;
                }
            }
            catch (OperationCanceledException)
            {
                Operation?.Invoke(this, new OperationEventArgs("[get-message/cancelled]"));
            }
            finally
            {
                Lock.Release();
            }

            return null;
        }

        private void Initialize()
        {
            Operation?.Invoke(this, new OperationEventArgs("[initialize/starting]"));

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

            using (_databaseContextService.BeginScope())
            using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                _databaseGateway.Execute(new Query(_scriptProvider.Get(_sqlQueueOptions.ConnectionStringName, Script.QueueRelease, Uri.QueueName))
                    .AddParameter(QueueColumns.UnacknowledgedHash, _unacknowledgedHash));
            }

            _initialized = true;

            Operation?.Invoke(this, new OperationEventArgs("[initialize/completed]"));
        }

        private async ValueTask<bool> IsEmptyAsync(bool sync)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[is-empty/cancelled]", true));
                return true;
            }

            Operation?.Invoke(this, new OperationEventArgs("[is-empty/starting]"));

            await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                using (_databaseContextService.BeginScope())
                await using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                {
                    var result = (sync
                        ? _databaseGateway.GetScalar<int>(_countQuery, _cancellationToken)
                        : await _databaseGateway.GetScalarAsync<int>(_countQuery, _cancellationToken).ConfigureAwait(false)) == 0;

                    Operation?.Invoke(this, new OperationEventArgs("[is-empty]", result));

                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                Operation?.Invoke(this, new OperationEventArgs("[is-empty/cancelled]", true));
            }
            finally
            {
                Lock.Release();
            }

            return true;
        }

        public async Task PurgeAsync(bool sync)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[purge/cancelled]"));
                return;
            }

            Operation?.Invoke(this, new OperationEventArgs("[purge/starting]"));

            await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                using (_databaseContextService.BeginScope())
                await using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                {
                    if (sync)
                    {
                        if (!ExistsAsync(true).GetAwaiter().GetResult())
                        {
                            return;
                        }

                        _databaseGateway.Execute(_purgeQuery, _cancellationToken);
                    }
                    else
                    {
                        if (!await ExistsAsync(false).ConfigureAwait(false))
                        {
                            return;
                        }

                        await _databaseGateway.ExecuteAsync(_purgeQuery, _cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Operation?.Invoke(this, new OperationEventArgs("[purge/cancelled]"));
            }
            finally
            {
                Lock.Release();
            }

            Operation?.Invoke(this, new OperationEventArgs("[purge/completed]"));
        }

        private async Task ReleaseAsync(object acknowledgementToken, bool sync)
        {
            Guard.AgainstNull(acknowledgementToken, nameof(acknowledgementToken));

            if (!(acknowledgementToken is int sequenceId) ||
                sequenceId <= 0)
            {
                return;
            }

            if (_cancellationToken.IsCancellationRequested)
            {
                Operation?.Invoke(this, new OperationEventArgs("[release/cancelled]"));
                return;
            }

            await Lock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                using (_databaseContextService.BeginScope())
                using (var databaseContext = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                using (var transaction = sync ? databaseContext.BeginTransaction() : await databaseContext.BeginTransactionAsync().ConfigureAwait(false))
                {
                    var row = sync
                        ? _databaseGateway.GetRow(
                            new Query(_dequeueIdQueryStatement)
                                .AddParameter(QueueColumns.SequenceId, sequenceId))
                        : await _databaseGateway.GetRowAsync(
                            new Query(_dequeueIdQueryStatement)
                                .AddParameter(QueueColumns.SequenceId, sequenceId)).ConfigureAwait(false);

                    if (row != null)
                    {
                        if (sync)
                        {
                            _databaseGateway.Execute(new Query(_removeQueryStatement)
                                .AddParameter(QueueColumns.SequenceId, sequenceId), _cancellationToken);

                            _databaseGateway.Execute(
                                new Query(_enqueueQueryStatement)
                                    .AddParameter(QueueColumns.MessageId, QueueColumns.MessageId.Value(row))
                                    .AddParameter(QueueColumns.MessageBody, row["MessageBody"]), _cancellationToken);

                            transaction.CommitTransaction();
                        }
                        else
                        {
                            await _databaseGateway.ExecuteAsync(new Query(_removeQueryStatement)
                                .AddParameter(QueueColumns.SequenceId, sequenceId), _cancellationToken).ConfigureAwait(false);

                            await _databaseGateway.ExecuteAsync(
                                new Query(_enqueueQueryStatement)
                                    .AddParameter(QueueColumns.MessageId, QueueColumns.MessageId.Value(row))
                                    .AddParameter(QueueColumns.MessageBody, row["MessageBody"]), _cancellationToken).ConfigureAwait(false);

                            await transaction.CommitTransactionAsync().ConfigureAwait(false);
                        }
                    }
                }

                MessageReleased?.Invoke(this, new MessageReleasedEventArgs(acknowledgementToken));
            }
            catch (OperationCanceledException)
            {
                Operation?.Invoke(this, new OperationEventArgs("[release/cancelled]"));
            }
            finally
            {
                Lock.Release();
            }
        }
    }
}