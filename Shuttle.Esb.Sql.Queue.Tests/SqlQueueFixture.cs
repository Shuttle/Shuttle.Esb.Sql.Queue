using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.Sql.Queue.Tests
{
    [TestFixture]
    public class SqlQueueFixture : BasicQueueFixture
    {
        [Test]
        public void Should_be_able_to_get_message_again_when_not_acknowledged_before_queue_is_disposed()
        {
            TestUnacknowledgedMessage(SqlFixture.GetServiceCollection(), "sql://shuttle/{0}");
        }

        [Test]
        public void Should_be_able_to_perform_simple_enqueue_and_get_message()
        {
            TestSimpleEnqueueAndGetMessage(SqlFixture.GetServiceCollection(), "sql://shuttle/{0}");
        }

        [Test]
        public void Should_be_able_to_release_a_message()
        {
            TestReleaseMessage(SqlFixture.GetServiceCollection(), "sql://shuttle/{0}");
        }
    }
}