using System;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue.SqlServer;

public class QueryFactory : IQueryFactory
{
    public IQuery GetMessage(string schema, string queueName, byte[] unacknowledgedHash)
    {
        Guard.AgainstNullOrEmptyString(schema);
        Guard.AgainstNullOrEmptyString(queueName);

        return new Query($@"
SET XACT_ABORT ON

DECLARE @HandleTransaction bit = 0

IF (@@trancount = 0)
BEGIN
	SET @HandleTransaction = 1
	BEGIN TRAN
END

UPDATE
	[{schema}].[{queueName}] 
SET
	UnacknowledgedHash = @UnacknowledgedHash,
	UnacknowledgedDate = SYSDATETIMEOFFSET(),
	UnacknowledgedId = @UnacknowledgedId
WHERE 
	SequenceId = 
	(
		SELECT TOP 1 
			SequenceId 
		FROM 
			[{schema}].[{queueName}] 
		WHERE 
			UnacknowledgedHash is null 
		ORDER BY 
			SequenceId
	);

SELECT 
	SequenceId, 
	MessageId, 
	MessageBody 
FROM 
	[{schema}].[{queueName}] 
WHERE 
	UnacknowledgedId = @UnacknowledgedId;

IF (@HandleTransaction = 1)
BEGIN
	commit tran
END
")
            .AddParameter(Columns.MachineName, Environment.MachineName)
            .AddParameter(Columns.QueueName, queueName)
            .AddParameter(Columns.UnacknowledgedHash, unacknowledgedHash)
            .AddParameter(Columns.UnacknowledgedId, Guid.NewGuid());
    }

    public IQuery Acknowledge(string schema, string queueName, long sequenceId)
    {
        Guard.AgainstNullOrEmptyString(schema);
        Guard.AgainstNullOrEmptyString(queueName);

        return new Query($"DELETE FROM [{schema}].[{queueName}] WHERE SequenceId = @SequenceId")
            .AddParameter(Columns.SequenceId, sequenceId);
    }

    public IQuery Create(string schema, string queueName)
    {
        Guard.AgainstNullOrEmptyString(schema);
        Guard.AgainstNullOrEmptyString(queueName);

        return new Query($@"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = '{schema}')
BEGIN
    EXEC('CREATE SCHEMA {schema}');
END

IF OBJECT_ID ('{schema}.{queueName}', 'U') IS NULL 
BEGIN
	CREATE TABLE [{schema}].[{queueName}]
    (
		[SequenceId] [bigint] IDENTITY(1,1) NOT NULL,
		[MessageId] [uniqueidentifier] NOT NULL,
		[MessageBody] [varbinary](max) NOT NULL,
		[UnacknowledgedHash] binary(16) NULL,
		[UnacknowledgedDate] datetimeoffset NULL,
		[UnacknowledgedId] [uniqueidentifier] NULL,
	    CONSTRAINT [PK_{queueName}] PRIMARY KEY CLUSTERED 
	    (
		    [SequenceId] ASC
	    ) 
        ON 
            [PRIMARY]
	) 
    ON 
        [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END

IF INDEXPROPERTY(OBJECT_ID('{schema}.{queueName}'), 'IX_{queueName}_UnacknowledgedId', 'IndexId') IS NULL
BEGIN
    CREATE NONCLUSTERED INDEX 
        [IX_{queueName}_UnacknowledgedId]
    ON 
        [{schema}].[{queueName}]
        (
            UnacknowledgedId
        ) 
    WITH
        ( 
            STATISTICS_NORECOMPUTE = OFF, 
            IGNORE_DUP_KEY = OFF, 
            ALLOW_ROW_LOCKS = ON, 
            ALLOW_PAGE_LOCKS = ON
        ) 
    ON 
        [PRIMARY]
END
");
    }

    public IQuery Drop(string schema, string queueName)
    {
        Guard.AgainstNullOrEmptyString(schema);
        Guard.AgainstNullOrEmptyString(queueName);

        return new Query($@"
IF OBJECT_ID(N'{schema}.{queueName}]', 'U') IS NOT NULL
BEGIN
    DROP TABLE [{schema}].[{queueName}]
END
");
    }

    public IQuery Enqueue(string schema, string queueName, Guid messageId, byte[] messageBody)
    {
        Guard.AgainstNullOrEmptyString(schema);
        Guard.AgainstNullOrEmptyString(queueName);

        return new Query($@"INSERT INTO [{schema}].[{queueName}] (MessageId, MessageBody) values (@MessageId, @MessageBody)")
            .AddParameter(Columns.MessageId, messageId)
            .AddParameter(Columns.MessageBody, messageBody);
    }

    public IQuery Exists(string schema, string queueName)
    {
        Guard.AgainstNullOrEmptyString(schema);
        Guard.AgainstNullOrEmptyString(queueName);

        return new Query($@"
IF OBJECT_ID('{schema}.{queueName}', 'U') IS NOT NULL
	SELECT 1
ELSE
	SELECT 0");
    }

    public IQuery Release(string schema, string queueName, byte[] unacknowledgedHash)
    {
        Guard.AgainstNullOrEmptyString(schema);
        Guard.AgainstNullOrEmptyString(queueName);

        return new Query($@"
IF (OBJECT_ID('{schema}.{queueName}', 'U') IS NULL)
    RETURN;

UPDATE
	[{schema}].[{queueName}] 
SET
	UnacknowledgedHash = null,
	UnacknowledgedDate = null,
	UnacknowledgedId = null
WHERE 
	UnacknowledgedHash = @UnacknowledgedHash
")
            .AddParameter(Columns.UnacknowledgedHash, unacknowledgedHash);
    }

    public IQuery Count(string schema, string queueName)
    {
        return new Query($"SELECT COUNT(*) FROM [{schema}].[{queueName}]");
    }

    public IQuery Purge(string schema, string queueName)
    {
        return new Query($"TRUNCATE TABLE [{schema}].[{queueName}]");
    }

    public IQuery Dequeue(string schema, string queueName, long sequenceId)
    {
        return new Query($"SELECT SequenceId, MessageId, MessageBody FROM [{schema}].[{queueName}] WHERE SequenceId = @SequenceId")
            .AddParameter(Columns.SequenceId, sequenceId);
    }

    public IQuery Remove(string schema, string queueName, long sequenceId)
    {
        return new Query($"DELETE FROM [{schema}].[{queueName}] WHERE SequenceId = @SequenceId")
            .AddParameter(Columns.SequenceId, sequenceId);
    }
}