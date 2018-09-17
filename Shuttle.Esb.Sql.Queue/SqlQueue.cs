using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;
using Shuttle.Core.Logging;
using Shuttle.Core.Streams;

namespace Shuttle.Esb.Sql.Queue
{
    public class SqlQueue : IQueue, ICreateQueue, IDropQueue, IPurgeQueue
    {
        private readonly string _connectionName;
        private readonly IDatabaseContextFactory _databaseContextFactory;
        private readonly IDatabaseGateway _databaseGateway;

        private readonly object _lock = new object();

        private readonly ILog _log;

        private readonly IScriptProvider _scriptProvider;
        private readonly string _tableName;
        private readonly byte[] _unacknowledgedHash;

        private IQuery _countQuery;
        private IQuery _createQuery;
        private string _dequeueIdQueryStatement;
        private IQuery _dropQuery;
        private string _enqueueQueryStatement;
        private IQuery _existsQuery;
        private IQuery _purgeQuery;

        private string _removeQueryStatement;

        public SqlQueue(Uri uri,
            IScriptProvider scriptProvider,
            IDatabaseContextFactory databaseContextFactory,
            IDatabaseGateway databaseGateway)
        {
            Guard.AgainstNull(uri, "uri");
            Guard.AgainstNull(scriptProvider, "scriptProvider");
            Guard.AgainstNull(databaseContextFactory, "databaseContextFactory");
            Guard.AgainstNull(databaseGateway, "databaseGateway");

            _scriptProvider = scriptProvider;
            _scriptProvider = scriptProvider;
            _databaseContextFactory = databaseContextFactory;
            _databaseGateway = databaseGateway;

            _log = Log.For(this);

            _unacknowledgedHash = MD5.Create()
                .ComputeHash(
                    Encoding.ASCII.GetBytes($@"{Environment.MachineName}\\{AppDomain.CurrentDomain.BaseDirectory}"));

            Uri = uri;

            var parser = new SqlUriParser(uri);

            _connectionName = parser.ConnectionName;
            _tableName = parser.TableName;

            Initialize();
        }

        public void Create()
        {
            using (_databaseContextFactory.Create(_connectionName))
            {
                _databaseGateway.ExecuteUsing(_createQuery);
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
                using (_databaseContextFactory.Create(_connectionName))
                {
                    _databaseGateway.ExecuteUsing(_dropQuery);
                }
            }
        }

        public void Purge()
        {
            if (!Exists())
            {
                return;
            }

            try
            {
                using (_databaseContextFactory.Create(_connectionName))
                {
                    _databaseGateway.ExecuteUsing(_purgeQuery);
                }
            }
            catch (Exception ex)
            {
                _log.Error(string.Format(Resources.PurgeError, Uri, ex.Message, _purgeQuery));

                throw;
            }
        }

        public void Enqueue(TransportMessage transportMessage, Stream stream)
        {
            using (_databaseContextFactory.Create(_connectionName))
            {
                _databaseGateway.ExecuteUsing(
                    RawQuery.Create(_enqueueQueryStatement)
                        .AddParameterValue(QueueColumns.MessageId, transportMessage.MessageId)
                        .AddParameterValue(QueueColumns.MessageBody, stream.ToBytes()));
            }
        }

        public Uri Uri { get; }

        public bool IsEmpty()
        {
            lock (_lock)
            {
                using (_databaseContextFactory.Create(_connectionName))
                {
                    return _databaseGateway.GetScalarUsing<int>(_countQuery) == 0;
                }
            }
        }

        public ReceivedMessage GetMessage()
        {
            using (_databaseContextFactory.Create(_connectionName))
            {
                var row = _databaseGateway.GetSingleRowUsing(
                    RawQuery.Create(_scriptProvider.Get(Script.QueueDequeue, _tableName))
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

            using (_databaseContextFactory.Create(_connectionName))
            {
                _databaseGateway.ExecuteUsing(RawQuery.Create(_removeQueryStatement)
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

            using (var connection = _databaseContextFactory.Create(_connectionName))
            using (var transaction = connection.BeginTransaction())
            {
                var row = _databaseGateway.GetSingleRowUsing(
                    RawQuery.Create(_dequeueIdQueryStatement)
                        .AddParameterValue(QueueColumns.SequenceId, sequenceId));

                if (row != null)
                {
                    _databaseGateway.ExecuteUsing(RawQuery.Create(_removeQueryStatement)
                        .AddParameterValue(QueueColumns.SequenceId, sequenceId));

                    _databaseGateway.ExecuteUsing(
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
                using (_databaseContextFactory.Create(_connectionName))
                {
                    return _databaseGateway.GetScalarUsing<int>(_existsQuery) == 1;
                }
            }
        }

        private void Initialize()
        {
            using (_databaseContextFactory.Create(_connectionName))
            {
                _existsQuery = RawQuery.Create(_scriptProvider.Get(Script.QueueExists, _tableName));
                _createQuery = RawQuery.Create(_scriptProvider.Get(Script.QueueCreate, _tableName));
                _dropQuery = RawQuery.Create(_scriptProvider.Get(Script.QueueDrop, _tableName));
                _purgeQuery = RawQuery.Create(_scriptProvider.Get(Script.QueuePurge, _tableName));
                _countQuery = RawQuery.Create(_scriptProvider.Get(Script.QueueCount, _tableName));
                _enqueueQueryStatement = _scriptProvider.Get(Script.QueueEnqueue, _tableName);
                _removeQueryStatement = _scriptProvider.Get(Script.QueueRemove, _tableName);
                _dequeueIdQueryStatement = _scriptProvider.Get(Script.QueueDequeueId, _tableName);
            }

            if (Exists())
            {
                Create();

                using (_databaseContextFactory.Create(_connectionName))
                {
                    _databaseGateway.ExecuteUsing(RawQuery.Create(_scriptProvider.Get(Script.QueueRelease, _tableName))
                        .AddParameterValue(QueueColumns.UnacknowledgedHash, _unacknowledgedHash));
                }
            }
        }
    }
}