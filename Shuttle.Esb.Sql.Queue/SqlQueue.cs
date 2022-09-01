using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;
using Shuttle.Core.Streams;

namespace Shuttle.Esb.Sql.Queue
{
    public class SqlQueue : IQueue, ICreateQueue, IDropQueue, IPurgeQueue
    {
        private readonly IDatabaseContextFactory _databaseContextFactory;
        private readonly IDatabaseGateway _databaseGateway;

        private readonly object _lock = new object();

        private readonly SqlQueueOptions _sqlQueueOptions;
        private readonly IScriptProvider _scriptProvider;
        private readonly byte[] _unacknowledgedHash;

        private IQuery _countQuery;
        private IQuery _createQuery;
        private string _dequeueIdQueryStatement;
        private IQuery _dropQuery;
        private string _enqueueQueryStatement;
        private IQuery _existsQuery;
        private IQuery _purgeQuery;

        private string _removeQueryStatement;
        private readonly string _machineName;
        private readonly string _baseDirectory;

        public SqlQueue(QueueUri uri, SqlQueueOptions sqlQueueOptions, IScriptProvider scriptProvider, IDatabaseContextFactory databaseContextFactory, IDatabaseGateway databaseGateway)
        {
            Guard.AgainstNull(uri, nameof(uri));
            Guard.AgainstNull(sqlQueueOptions, nameof(sqlQueueOptions));
            Guard.AgainstNull(scriptProvider, nameof(scriptProvider));
            Guard.AgainstNull(databaseContextFactory, nameof(databaseContextFactory));
            Guard.AgainstNull(databaseGateway, nameof(databaseGateway));

            _sqlQueueOptions = sqlQueueOptions;
            _scriptProvider = scriptProvider;
            _scriptProvider = scriptProvider;
            _databaseContextFactory = databaseContextFactory;
            _databaseGateway = databaseGateway;

            _machineName = Environment.MachineName;
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _unacknowledgedHash = MD5.Create()
                .ComputeHash(
                    Encoding.ASCII.GetBytes($@"{_machineName}\\{_baseDirectory}"));

            Uri = uri;

            Initialize();
        }

        public void Create()
        {
            using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                _databaseGateway.Execute(_createQuery);
            }
        }

        public void Drop()
        {
            if (!Exists())
            {
                return;
            }

            lock (_lock)
            {
                using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                {
                    _databaseGateway.Execute(_dropQuery);
                }
            }
        }

        public void Purge()
        {
            if (!Exists())
            {
                return;
            }

            using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                _databaseGateway.Execute(_purgeQuery);
            }
        }

        public void Enqueue(TransportMessage transportMessage, Stream stream)
        {
            using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                _databaseGateway.Execute(
                    RawQuery.Create(_enqueueQueryStatement)
                        .AddParameterValue(QueueColumns.MessageId, transportMessage.MessageId)
                        .AddParameterValue(QueueColumns.MessageBody, stream.ToBytes()));
            }
        }

        public QueueUri Uri { get; }
        public bool IsStream => false;

        public bool IsEmpty()
        {
            lock (_lock)
            {
                using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                {
                    return _databaseGateway.GetScalar<int>(_countQuery) == 0;
                }
            }
        }

        public ReceivedMessage GetMessage()
        {
            using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                var row = _databaseGateway.GetRow(
                    RawQuery.Create(_scriptProvider.Get(Script.QueueDequeue, Uri.QueueName))
                        .AddParameterValue(QueueColumns.MachineName, _machineName)
                        .AddParameterValue(QueueColumns.QueueName, Uri.QueueName)
                        .AddParameterValue(QueueColumns.UnacknowledgedHash, _unacknowledgedHash)
                        .AddParameterValue(QueueColumns.UnacknowledgedId, Guid.NewGuid()));

                if (row == null)
                {
                    return null;
                }

                var result = new MemoryStream((byte[]) row["MessageBody"]);

                return new ReceivedMessage(result, QueueColumns.SequenceId.MapFrom(row));
            }
        }

        public void Acknowledge(object acknowledgementToken)
        {
            var sequenceId = (int) acknowledgementToken;

            if (sequenceId == 0)
            {
                return;
            }

            using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                _databaseGateway.Execute(RawQuery.Create(_removeQueryStatement)
                    .AddParameterValue(QueueColumns.SequenceId, sequenceId));
            }
        }

        public void Release(object acknowledgementToken)
        {
            var sequenceId = (int) acknowledgementToken;

            if (sequenceId <= 0)
            {
                return;
            }

            using (var connection = _databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            using (var transaction = connection.BeginTransaction())
            {
                var row = _databaseGateway.GetRow(
                    RawQuery.Create(_dequeueIdQueryStatement)
                        .AddParameterValue(QueueColumns.SequenceId, sequenceId));

                if (row != null)
                {
                    _databaseGateway.Execute(RawQuery.Create(_removeQueryStatement)
                        .AddParameterValue(QueueColumns.SequenceId, sequenceId));

                    _databaseGateway.Execute(
                        RawQuery.Create(_enqueueQueryStatement)
                            .AddParameterValue(QueueColumns.MessageId, QueueColumns.MessageId.MapFrom(row))
                            .AddParameterValue(QueueColumns.MessageBody, row["MessageBody"]));
                }

                transaction.CommitTransaction();
            }
        }

        private bool Exists()
        {
            lock (_lock)
            {
                using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
                {
                    return _databaseGateway.GetScalar<int>(_existsQuery) == 1;
                }
            }
        }

        private void Initialize()
        {
            using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                _createQuery = RawQuery.Create(_scriptProvider.Get(Script.QueueCreate, Uri.QueueName))
                    .AddParameterValue(QueueColumns.UnacknowledgedHash, _unacknowledgedHash)
                    .AddParameterValue(QueueColumns.MachineName, _machineName)
                    .AddParameterValue(QueueColumns.QueueName, Uri.QueueName)
                    .AddParameterValue(QueueColumns.BaseDirectory, _baseDirectory);

                _existsQuery = RawQuery.Create(_scriptProvider.Get(Script.QueueExists, Uri.QueueName));
                _dropQuery = RawQuery.Create(_scriptProvider.Get(Script.QueueDrop, Uri.QueueName));
                _purgeQuery = RawQuery.Create(_scriptProvider.Get(Script.QueuePurge, Uri.QueueName));
                _countQuery = RawQuery.Create(_scriptProvider.Get(Script.QueueCount, Uri.QueueName));
                _enqueueQueryStatement = _scriptProvider.Get(Script.QueueEnqueue, Uri.QueueName);
                _removeQueryStatement = _scriptProvider.Get(Script.QueueRemove, Uri.QueueName);
                _dequeueIdQueryStatement = _scriptProvider.Get(Script.QueueDequeueId, Uri.QueueName);
            }

            Create();

            using (_databaseContextFactory.Create(_sqlQueueOptions.ConnectionStringName))
            {
                _databaseGateway.Execute(RawQuery.Create(_scriptProvider.Get(Script.QueueRelease, Uri.QueueName))
                    .AddParameterValue(QueueColumns.UnacknowledgedHash, _unacknowledgedHash));
            }
        }
    }
}