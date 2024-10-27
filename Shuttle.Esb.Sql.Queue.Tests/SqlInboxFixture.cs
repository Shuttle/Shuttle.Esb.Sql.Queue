using System.Threading.Tasks;
using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.Sql.Queue.Tests;

public class SqlInboxFixture : InboxFixture
{
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public async Task Should_be_able_handle_errors_async(bool hasErrorQueue, bool isTransactionalEndpoint)
    {
        await TestInboxErrorAsync(SqlConfiguration.GetServiceCollection(), "sql://shuttle/{0}", hasErrorQueue, isTransactionalEndpoint);
    }

    [Test]
    [TestCase(500, false)]
    [TestCase(500, true)]
    public async Task Should_be_able_to_process_messages_concurrently_async(int msToComplete, bool isTransactionalEndpoint)
    {
        await TestInboxConcurrencyAsync(SqlConfiguration.GetServiceCollection(), "sql://shuttle/{0}", msToComplete, false);
    }

    [Test]
    [TestCase(50, false)]
    [TestCase(50, true)]
    public async Task Should_be_able_to_process_queue_timeously_async(int count, bool isTransactionalEndpoint)
    {
        await TestInboxThroughputAsync(SqlConfiguration.GetServiceCollection(), "sql://shuttle/{0}", 1000, count, 5, isTransactionalEndpoint);
    }
}