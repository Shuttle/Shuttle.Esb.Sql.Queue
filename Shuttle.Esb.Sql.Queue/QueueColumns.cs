using System;
using System.Data;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue;

public class QueueColumns
{
    public static Column<string> BaseDirectory = new("BaseDirectory", DbType.AnsiString);
    public static Column<byte[]> UnacknowledgedHash = new("UnacknowledgedHash", DbType.Binary);
    public static Column<string> MachineName = new("MachineName", DbType.AnsiString);
    public static Column<string> QueueName = new("QueueName", DbType.AnsiString);
    public static Column<byte[]> MessageBody = new("MessageBody", DbType.Binary);
    public static Column<Guid> MessageId = new("MessageId", DbType.Guid);
    public static Column<int> SequenceId = new("SequenceId", DbType.Int32);
    public static Column<DateTime?> UnacknowledgedDate = new("UnacknowledgedDate", DbType.DateTime);
    public static Column<Guid?> UnacknowledgedId = new("UnacknowledgedId", DbType.Guid);
}