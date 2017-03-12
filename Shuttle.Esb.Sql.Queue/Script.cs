namespace Shuttle.Esb.Sql.Queue
{
	public class Script
	{
		public static readonly string QueueCount = "QueueCount";
		public static readonly string QueueCreate = "QueueCreate";
		public static readonly string QueueDequeue = "QueueDequeue";
		public static readonly string QueueDequeueId = "QueueDequeueId";
		public static readonly string QueueDrop = "QueueDrop";
		public static readonly string QueueEnqueue = "QueueEnqueue";
		public static readonly string QueueExists = "QueueExists";
		public static readonly string QueuePurge = "QueuePurge";
		public static readonly string QueueRemove = "QueueRemove";
		public static readonly string QueueRead = "QueueRead";

		public static readonly string DeferredMessageExists = "DeferredMessageExists";
		public static readonly string DeferredMessageEnqueue = "DeferredMessageEnqueue";
		public static readonly string DeferredMessageDequeue = "DeferredMessageDequeue";
		public static readonly string DeferredMessagePurge = "DeferredMessagePurge";
		public static readonly string DeferredMessageCount = "DeferredMessageCount";
	}
}