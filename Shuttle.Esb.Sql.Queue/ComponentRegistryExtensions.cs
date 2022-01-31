using Shuttle.Core.Container;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue
{
	public static class ComponentRegistryExtensions
	{
		public static void RegisterSqlQueue(this IComponentRegistry registry)
		{
			Guard.AgainstNull(registry, "registry");

			registry.AttemptRegister<IScriptProviderConfiguration, ScriptProviderConfiguration>();
			registry.AttemptRegister<IScriptProvider, ScriptProvider>();

			registry.RegisterDataAccess();
		}
	}
}