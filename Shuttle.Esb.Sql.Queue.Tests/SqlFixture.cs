using System.Data.Common;
using System.Data.SqlClient;
using Castle.Windsor;
using NUnit.Framework;
using Shuttle.Core.Castle;
using Shuttle.Core.Data;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.Sql.Queue.Tests
{
	[SetUpFixture]
	public class SqlFixture
	{
		public static ComponentContainer GetComponentContainer()
		{
			var container = new WindsorComponentContainer(new WindsorContainer());

			container.RegisterSqlQueue();
			container.RegisterDataAccess();

			return new ComponentContainer(container, () => container);
		}

		[OneTimeSetUp]
		public void OneTimeSetup()
		{
			DbProviderFactories.RegisterFactory("System.Data.SqlClient", SqlClientFactory.Instance);
		}
	}
}