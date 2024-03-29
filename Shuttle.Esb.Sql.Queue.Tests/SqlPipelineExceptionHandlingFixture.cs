using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.Sql.Queue.Tests
{
	public class SqlPipelineExceptionHandlingFixture : PipelineExceptionFixture
	{
		[Test]
		public void Should_be_able_to_handle_exceptions_in_receive_stage_of_receive_pipeline()
		{
			TestExceptionHandling(SqlFixture.GetServiceCollection(), "sql://shuttle/{0}");
		}
	}
}