using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.Sql.Queue.Tests
{
	public class SqlInboxFixture : InboxFixture
	{
		[TestCase(true, true)]
		[TestCase(true, false)]
		[TestCase(false, true)]
		[TestCase(false, false)]
		public void Should_be_able_handle_errors(bool hasErrorQueue, bool isTransactionalEndpoint)
		{
			TestInboxError(SqlFixture.GetServiceCollection(), "sql://shuttle/{0}", hasErrorQueue, isTransactionalEndpoint);
		}

		[Test]
		[TestCase(500, false)]
		[TestCase(500, true)]
		public void Should_be_able_to_process_messages_concurrently(int msToComplete, bool isTransactionalEndpoint)
		{
			TestInboxConcurrency(SqlFixture.GetServiceCollection(), "sql://shuttle/{0}", msToComplete, false);
		}

		[Test]
		[TestCase(200, false)]
		[TestCase(200, true)]
		public void Should_be_able_to_process_queue_timeously(int count, bool isTransactionalEndpoint)
		{
			TestInboxThroughput(SqlFixture.GetServiceCollection(), "sql://shuttle/{0}", 1000, count, 5, isTransactionalEndpoint);
		}
	}
}