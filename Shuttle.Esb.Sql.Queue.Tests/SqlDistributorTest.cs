using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.Sql.Queue.Tests
{
	public class SqlDistributorTest : DistributorFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_be_able_to_distribute_messages(bool isTransactionalEndpoint)
		{
			TestDistributor(SqlFixture.GetComponentContainer(), SqlFixture.GetComponentContainer(), @"sql://shuttle/{0}", isTransactionalEndpoint);
		}
	}
}