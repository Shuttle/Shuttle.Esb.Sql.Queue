using Moq;
using Shuttle.Core.Container;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;
#if (NETCOREAPP2_0 || NETSTANDARD2_0)
using Shuttle.Core.Data.SqlClient;
#endif

namespace Shuttle.Esb.Sql.Queue.Tests
{
    public class Bootstrap : IComponentRegistryBootstrap
    {
        public void Register(IComponentRegistry registry)
        {
            Guard.AgainstNull(registry, nameof(registry));

#if (NETCOREAPP2_0 || NETSTANDARD2_0)
            registry.Register<IDbProviderFactories, DbProviderFactories>();

            var connectionConfigurationProvider = new Mock<IConnectionConfigurationProvider>();

            connectionConfigurationProvider.Setup(m => m.Get(It.IsAny<string>())).Returns(
                new ConnectionConfiguration(
                    "Shuttle",
                    "System.Data.SqlClient",
                    "Data Source=.\\sqlexpress;Initial Catalog=shuttle;Integrated Security=SSPI;"));

            registry.RegisterInstance(connectionConfigurationProvider.Object);
#else
            registry.Register<IConnectionConfigurationProvider, ConnectionConfigurationProvider>();
#endif
        }
    }
}