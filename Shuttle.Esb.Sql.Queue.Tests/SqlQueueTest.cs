using NUnit.Framework;
using Shuttle.Core.Data;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.Sql.Queue.Tests
{
    [TestFixture]
    public class SqlQueueTest : BasicQueueFixture
    {
        [Test]
        public void Should_be_able_to_get_message_again_when_not_acknowledged_before_queue_is_disposed()
        {
            TestUnacknowledgedMessage(SqlFixture.GetComponentContainer(), "sql://shuttle/{0}");
        }

        [Test]
        public void Should_be_able_to_perform_simple_enqueue_and_get_message()
        {
            TestSimpleEnqueueAndGetMessage(SqlFixture.GetComponentContainer(), "sql://shuttle/{0}");
        }

        [Test]
        public void Should_be_able_to_release_a_message()
        {
            TestReleaseMessage(SqlFixture.GetComponentContainer(), "sql://shuttle/{0}");
        }
    }
}