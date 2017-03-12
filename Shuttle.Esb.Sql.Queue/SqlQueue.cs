using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shuttle.Core.Data;
using Shuttle.Core.Infrastructure;

namespace Shuttle.Esb.Sql.Queue
{
	public class SqlQueue : IQueue, ICreateQueue, IDropQueue, IPurgeQueue
	{
		private class UnacknowledgedMessage
		{
			public UnacknowledgedMessage(Guid messageId, int sequenceId)
			{
				SequenceId = sequenceId;
				MessageId = messageId;
			}

			public int SequenceId { get; private set; }
			public Guid MessageId { get; private set; }
		}

		private readonly IDatabaseGateway _databaseGateway;

		private readonly object _padlock = new object();
		private readonly List<UnacknowledgedMessage> _unacknowledgedMessages = new List<UnacknowledgedMessage>();

		private readonly List<Guid> _emptyMessageIds = new List<Guid>
		{
			Guid.Empty
		};

		private readonly IScriptProvider _scriptProvider;

		private readonly string _connectionName;
		private readonly string _tableName;
		private readonly IDatabaseContextFactory _databaseContextFactory;

		private IQuery _countQuery;
		private IQuery _createQuery;
		private IQuery _dropQuery;
		private IQuery _existsQuery;
		private IQuery _purgeQuery;

		private string _removeQueryStatement;
		private string _enqueueQueryStatement;
		private string _dequeueIdQueryStatement;

		private readonly ILog _log;

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

			try
			{
				using (_databaseContextFactory.Create(_connectionName))
				{
					_databaseGateway.ExecuteUsing(_createQuery);
				}
			}
			catch (Exception ex)
			{
				_log.Error(string.Format(SqlQueueResources.CreateError, Uri, ex.Message, _createQuery));

				throw;
			}
		}

		public void Drop()
		{
			if (!Exists())
			{
				return;
			}

			try
			{
				lock (_padlock)
				{
					using (_databaseContextFactory.Create(_connectionName))
					{
						_databaseGateway.ExecuteUsing(_dropQuery);
					}

					ResetUnacknowledgedMessageIds();
				}
			}
			catch (Exception ex)
			{
				_log.Error(string.Format(SqlQueueResources.DropError, Uri, ex.Message, _dropQuery));

				throw;
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
				lock (_padlock)
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
				_log.Error(string.Format(SqlQueueResources.PurgeError, Uri, ex.Message, _purgeQuery));

				throw;
			}
		}

		public void Enqueue(TransportMessage transportMessage, Stream stream)
		{
			try
			{
				using (_databaseContextFactory.Create(_connectionName))
				{
					_databaseGateway.ExecuteUsing(
						RawQuery.Create(_enqueueQueryStatement)
							.AddParameterValue(QueueColumns.MessageId, transportMessage.MessageId)
							.AddParameterValue(QueueColumns.MessageBody, stream.ToBytes()));
				}
			}
			catch (Exception ex)
			{
				_log.Error(
					string.Format(SqlQueueResources.EnqueueError, transportMessage.MessageId, Uri, ex.Message));

				throw;
			}
		}

		public Uri Uri { get; private set; }

		private bool Exists()
		{
			try
			{
				using (_databaseContextFactory.Create(_connectionName))
				{
					return _databaseGateway.GetScalarUsing<int>(_existsQuery) == 1;
				}
			}
			catch (Exception ex)
			{
				_log.Error(string.Format(SqlQueueResources.ExistsError, Uri, ex.Message, _existsQuery));

				throw;
			}
		}

		public bool IsEmpty()
		{
			try
			{
				using (_databaseContextFactory.Create(_connectionName))
				{
					return _databaseGateway.GetScalarUsing<int>(_countQuery) == 0;
				}
			}
			catch (Exception ex)
			{
				_log.Error(string.Format(SqlQueueResources.CountError, Uri, ex.Message, _countQuery));

				return true;
			}
		}

		public ReceivedMessage GetMessage()
		{
			lock (_padlock)
			{
				try
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
									string.Join(",", messageIds.Select(id => string.Format("'{0}'", id)).ToArray()))));

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
				catch (Exception ex)
				{
					_log.Error(string.Format(SqlQueueResources.DequeueError, Uri, ex.Message, _createQuery));

					throw;
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

		public void Acknowledge(object acknowledgementToken)
		{
			try
			{
				var messageId = (Guid) acknowledgementToken;

				lock (_padlock)
				{
					using (_databaseContextFactory.Create(_connectionName))
					{
						_databaseGateway.ExecuteUsing(RawQuery.Create(_removeQueryStatement)
							.AddParameterValue(QueueColumns.SequenceId, SequenceId(messageId)));

						MessageIdAcknowledged(messageId);
					}
				}
			}
			catch (Exception ex)
			{
				_log.Error(string.Format(SqlQueueResources.RemoveError, Uri, ex.Message, _removeQueryStatement));

				throw;
			}
		}

		public void Release(object acknowledgementToken)
		{
			try
			{
				var messageId = (Guid) acknowledgementToken;

				lock (_padlock)
				{
					using (var connection = _databaseContextFactory.Create(_connectionName))
					using (var transaction = connection.BeginTransaction())
					{
						var row = _databaseGateway.GetSingleRowUsing(
							RawQuery.Create(_dequeueIdQueryStatement).AddParameterValue(QueueColumns.MessageId, messageId));

						if (row != null)
						{
							_databaseGateway.ExecuteUsing(RawQuery.Create(_removeQueryStatement)
								.AddParameterValue(QueueColumns.SequenceId, SequenceId(messageId)));

							_databaseGateway.ExecuteUsing(
								RawQuery.Create(_enqueueQueryStatement)
									.AddParameterValue(QueueColumns.MessageId, messageId)
									.AddParameterValue(QueueColumns.MessageBody, row["MessageBody"]));
						}

						MessageIdAcknowledged(messageId);

						transaction.CommitTransaction();
					}
				}
			}
			catch (Exception ex)
			{
				_log.Error(string.Format(SqlQueueResources.RemoveError, Uri, ex.Message, _removeQueryStatement));

				throw;
			}
		}

		private void MessageIdAcknowledged(Guid messageId)
		{
			_unacknowledgedMessages.RemoveAll(unacknowledgedMessage => unacknowledgedMessage.MessageId.Equals(messageId));
		}

		private int SequenceId(Guid messageId)
		{
			var unacknowledgedMessage = _unacknowledgedMessages.Find(candidate => candidate.MessageId.Equals(messageId));

			return unacknowledgedMessage != null
				? unacknowledgedMessage.SequenceId
				: 0;
		}

		private void ResetUnacknowledgedMessageIds()
		{
			_unacknowledgedMessages.Clear();
		}
	}
}