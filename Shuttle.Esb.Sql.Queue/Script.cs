using System.Configuration;
using Shuttle.Core.Infrastructure;

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

		private Script(string name)
		{
			Name = name;

			var key = string.Format("{0}FileName", name);

			var value = ConfigurationManager.AppSettings[key];

			if (!string.IsNullOrEmpty(value))
			{
				FileName = value;

				return;
			}

			FileName = string.Format("{0}.sql", name);

			Log.For(this)
				.Information(
					string.Format(
						"The application configuration AppSettings section does not contain a key '{0}'.  Using default value of '{1}'.",
						key, FileName));
		}

		public string Name { get; private set; }
		public string FileName { get; private set; }
	}
}