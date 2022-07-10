using System.Data.Common;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue.Tests
{
	[SetUpFixture]
	public class SqlFixture
	{
		public static IServiceCollection GetServiceCollection()
		{
			var services = new ServiceCollection();

			services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
			services.AddDataAccess(builder =>
			{
				builder.AddConnectionString("shuttle", "System.Data.SqlClient", "server=.;database=shuttle;user id=sa;password=Pass!000");
			});
			services.AddSqlQueue();

			return services;
		}

		[OneTimeSetUp]
		public void OneTimeSetup()
		{
			DbProviderFactories.RegisterFactory("System.Data.SqlClient", SqlClientFactory.Instance);
		}
	}
}