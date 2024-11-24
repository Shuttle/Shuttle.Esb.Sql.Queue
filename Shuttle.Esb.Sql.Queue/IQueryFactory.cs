using System;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue;

public interface IQueryFactory
{
    IQuery GetMessage(string schema, string queueName, byte[] unacknowledgedHash);
    IQuery Acknowledge(string schema, string queueName, long sequenceId);
    IQuery Create(string schema, string queueName);
    IQuery Drop(string schema, string queueName);
    IQuery Enqueue(string schema, string queueName, Guid messageId, byte[] messageBody);
    IQuery Exists(string schema, string queueName);
    IQuery Release(string schema, string queueName, byte[] unacknowledgedHash);
    IQuery Count(string schema, string queueName);
    IQuery Purge(string schema, string queueName);
    IQuery Dequeue(string schema, string queueName, long sequenceId);
    IQuery Remove(string schema, string queueName, long sequenceId);
}