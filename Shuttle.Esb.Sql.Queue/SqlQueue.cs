using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private readonly List<Guid> _emptyMessageIds = new List<Guid>
        {
            Guid.Empty
        };

        private readonly object _lock = new object();

        private readonly ILog _log;

        private readonly IScriptProvider _scriptProvider;
        private readonly string _tableName;
        private readonly List<UnacknowledgedMessage> _unacknowledgedMessages = new List<UnacknowledgedMessage>();

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
            _databaseContextFactory = databaseContextFactory;
            _databaseGateway = databaseGateway;

            _log = Log.For(this);

            Uri = uri;

            var parser = new SqlUriParser(uri);

            _connectionName = parser.ConnectionName;
            _tableName = parser.TableName;

            BuildQueries();
        }

        public void Create()
        {
            if (Exists())
            {
                return;
            }

            lock (_lock)
            {
                using (_databaseContextFactory.Create(_connectionName))
                {
                    _databaseGateway.ExecuteUsing(_createQuery);
                }
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

                ResetUnacknowledgedMessageIds();
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
                lock (_lock)
                {
                    using (_databaseContextFactory.Create(_connectionName))
                    {
                        _databaseGateway.ExecuteUsing(_purgeQuery);
                    }

                    ResetUnacknowledgedMessageIds();
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
            lock (_lock)
            {
                using (_databaseContextFactory.Create(_connectionName))
                {
                    _databaseGateway.ExecuteUsing(
                        RawQuery.Create(_enqueueQueryStatement)
                            .AddParameterValue(QueueColumns.MessageId, transportMessage.MessageId)
                            .AddParameterValue(QueueColumns.MessageBody, stream.ToBytes()));
                }
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
            lock (_lock)
            {
                using (_databaseContextFactory.Create(_connectionName))
                {
                    var messageIds = _unacknowledgedMessages.Count > 0
                        ? _unacknowledgedMessages.Select(unacknowledgedMessage => unacknowledgedMessage.MessageId)
                        : _emptyMessageIds;

                    var row = _databaseGateway.GetSingleRowUsing(
                        RawQuery.Create(
                            _scriptProvider.Get(
                                Script.QueueDequeue,
                                _tableName,
                                string.Join(",", messageIds.Select(id => $"'{id}'").ToArray()))));

                    if (row == null)
                    {
                        return null;
                    }

                    var result = new MemoryStream((byte[]) row["MessageBody"]);
                    var messageId = new Guid(row["MessageId"].ToString());

                    MessageIdAcknowledgementRequired((int) row["SequenceId"], messageId);

                    return new ReceivedMessage(result, messageId);
                }
            }
        }

        public void Acknowledge(object acknowledgementToken)
        {
            var messageId = (Guid) acknowledgementToken;

            lock (_lock)
            {
                var sequenceId = MessageIdAcknowledged(messageId);

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
        }

        public void Release(object acknowledgementToken)
        {
            var messageId = (Guid) acknowledgementToken;

            lock (_lock)
            {
                var sequenceId = MessageIdAcknowledged(messageId);

                if (sequenceId <= 0)
                {
                    return;
                }

                using (var connection = _databaseContextFactory.Create(_connectionName))
                using (var transaction = connection.BeginTransaction())
                {
                    var row = _databaseGateway.GetSingleRowUsing(
                        RawQuery.Create(_dequeueIdQueryStatement)
                            .AddParameterValue(QueueColumns.MessageId, messageId));

                    if (row != null)
                    {
                        _databaseGateway.ExecuteUsing(RawQuery.Create(_removeQueryStatement)
                            .AddParameterValue(QueueColumns.SequenceId, sequenceId));

                        _databaseGateway.ExecuteUsing(
                            RawQuery.Create(_enqueueQueryStatement)
                                .AddParameterValue(QueueColumns.MessageId, messageId)
                                .AddParameterValue(QueueColumns.MessageBody, row["MessageBody"]));
                    }

                    transaction.CommitTransaction();
                }
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

        private void MessageIdAcknowledgementRequired(int sequenceId, Guid messageId)
        {
            _unacknowledgedMessages.Add(new UnacknowledgedMessage(messageId, sequenceId));
        }

        private void BuildQueries()
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
        }

        private int MessageIdAcknowledged(Guid messageId)
        {
            var unacknowledgedMessage =
                _unacknowledgedMessages.Find(candidate => candidate.MessageId.Equals(messageId));

            _unacknowledgedMessages.RemoveAll(message => message.MessageId.Equals(messageId));

            return unacknowledgedMessage?.SequenceId ?? 0;
        }

        private void ResetUnacknowledgedMessageIds()
        {
            _unacknowledgedMessages.Clear();
        }

        private class UnacknowledgedMessage
        {
            public UnacknowledgedMessage(Guid messageId, int sequenceId)
            {
                SequenceId = sequenceId;
                MessageId = messageId;
            }

            public int SequenceId { get; }
            public Guid MessageId { get; }
        }
    }
}