using System;
using System.Data;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue
{
	public class QueueColumns
	{
		public static MappedColumn<int> SequenceId = new MappedColumn<int>("SequenceId", DbType.Int32);
		public static MappedColumn<Guid> MessageId = new MappedColumn<Guid>("MessageId", DbType.Guid);
		public static MappedColumn<byte[]> MessageBody = new MappedColumn<byte[]>("MessageBody", DbType.Binary);
		public static MappedColumn<byte[]> UnacknowledgedHash = new MappedColumn<byte[]>("UnacknowledgedHash", DbType.Binary);
	    public static MappedColumn<DateTime?> UnacknowledgedDate = new MappedColumn<DateTime?>("UnacknowledgedDate", DbType.DateTime);
	    public static MappedColumn<Guid?> UnacknowledgedId = new MappedColumn<Guid?>("UnacknowledgedId", DbType.Guid);
	}
}