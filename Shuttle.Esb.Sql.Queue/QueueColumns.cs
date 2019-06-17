using System;
using System.Data;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue
{
	public class QueueColumns
	{
        public static MappedColumn<string> BaseDirectory = new MappedColumn<string>("BaseDirectory", DbType.AnsiString);
        public static MappedColumn<byte[]> EndpointHash = new MappedColumn<byte[]>("EndpointHash", DbType.Binary);
        public static MappedColumn<string> MachineName = new MappedColumn<string>("MachineName", DbType.AnsiString);
        public static MappedColumn<byte[]> MessageBody = new MappedColumn<byte[]>("MessageBody", DbType.Binary);
		public static MappedColumn<Guid> MessageId = new MappedColumn<Guid>("MessageId", DbType.Guid);
        public static MappedColumn<int> SequenceId = new MappedColumn<int>("SequenceId", DbType.Int32);
	    public static MappedColumn<DateTime?> UnacknowledgedDate = new MappedColumn<DateTime?>("UnacknowledgedDate", DbType.DateTime);
	    public static MappedColumn<Guid?> UnacknowledgedId = new MappedColumn<Guid?>("UnacknowledgedId", DbType.Guid);
	}
}