using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shuttle.Core.Data;
using Shuttle.Core.Data.Logging;

namespace Shuttle.Esb.Sql.Queue.Tests;

[SetUpFixture]
public class SqlConfiguration
{
    public static IServiceCollection GetServiceCollection()
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddDataAccess(builder =>
            {
                builder.AddConnectionString("shuttle", "Microsoft.Data.SqlClient", "server=.;database=shuttle;user id=sa;password=Pass!000;TrustServerCertificate=true");
            })
            .AddDataAccessLogging(builder =>
            {
                builder.Options.DatabaseContext = false;
                builder.Options.DbCommandFactory = true;
            })
            .AddSqlQueue(builder =>
            {
                builder.AddOptions("shuttle", new SqlQueueOptions
                {
                    ConnectionStringName = "shuttle"
                });
            });

        return services;
    }

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
    }
}