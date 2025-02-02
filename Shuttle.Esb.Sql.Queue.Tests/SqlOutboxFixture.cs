using System.Threading.Tasks;
using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.Sql.Queue.Tests;

public class SqlOutboxFixture : OutboxFixture
{
    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public async Task Should_be_able_handle_errors_async(bool isTransactionalEndpoint)
    {
        await TestOutboxSendingAsync(SqlConfiguration.GetServiceCollection(), "sql://shuttle/{0}", 1, isTransactionalEndpoint);
    }
}