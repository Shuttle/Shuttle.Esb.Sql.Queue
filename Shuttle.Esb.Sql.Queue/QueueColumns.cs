using System;
using System.Data;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue
{
	public class QueueColumns
	{
        public static Column<string> BaseDirectory = new Column<string>("BaseDirectory", DbType.AnsiString);
        public static Column<byte[]> UnacknowledgedHash = new Column<byte[]>("UnacknowledgedHash", DbType.Binary);
        public static Column<string> MachineName = new Column<string>("MachineName", DbType.AnsiString);
        public static Column<string> QueueName = new Column<string>("QueueName", DbType.AnsiString);
        public static Column<byte[]> MessageBody = new Column<byte[]>("MessageBody", DbType.Binary);
		public static Column<Guid> MessageId = new Column<Guid>("MessageId", DbType.Guid);
        public static Column<int> SequenceId = new Column<int>("SequenceId", DbType.Int32);
	    public static Column<DateTime?> UnacknowledgedDate = new Column<DateTime?>("UnacknowledgedDate", DbType.DateTime);
	    public static Column<Guid?> UnacknowledgedId = new Column<Guid?>("UnacknowledgedId", DbType.Guid);
	}
}