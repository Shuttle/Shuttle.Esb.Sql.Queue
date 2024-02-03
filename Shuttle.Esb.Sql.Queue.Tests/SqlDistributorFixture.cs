using System.Threading.Tasks;
using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.Sql.Queue.Tests
{
	public class SqlDistributorFixture : DistributorFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public void Should_be_able_to_distribute_messages(bool isTransactionalEndpoint)
		{
			TestDistributor(SqlConfiguration.GetServiceCollection(), SqlConfiguration.GetServiceCollection(), @"sql://shuttle/{0}", isTransactionalEndpoint);
		}

		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public async Task Should_be_able_to_distribute_messages_async(bool isTransactionalEndpoint)
		{
			await TestDistributorAsync(SqlConfiguration.GetServiceCollection(), SqlConfiguration.GetServiceCollection(), @"sql://shuttle/{0}", isTransactionalEndpoint);
		}
	}
}