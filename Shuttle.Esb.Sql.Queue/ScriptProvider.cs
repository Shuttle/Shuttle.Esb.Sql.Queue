using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue
{
	public class ScriptProvider : IScriptProvider
	{
		private readonly Core.Data.IScriptProvider _scriptProvider;

		public ScriptProvider(IOptions<ScriptProviderOptions> options, IDatabaseContextCache databaseContextCache)
		{
			Guard.AgainstNull(options, nameof(options));
			Guard.AgainstNull(options.Value, nameof(options.Value));
			Guard.AgainstNull(databaseContextCache, nameof(databaseContextCache));

			_scriptProvider = new Core.Data.ScriptProvider(Options.Create(new ScriptProviderOptions
			{
				ResourceNameFormat = string.IsNullOrEmpty(options.Value.ResourceNameFormat)
					? "Shuttle.Esb.Sql.Queue..scripts.{ProviderName}.{ScriptName}.sql"
					: options.Value.ResourceNameFormat,
				ResourceAssembly = options.Value.ResourceAssembly ?? typeof(SqlQueue).Assembly,
				FileNameFormat = options.Value.FileNameFormat,
				ScriptFolder = options.Value.ScriptFolder
			}), databaseContextCache);
		}

		public string Get(string scriptName)
		{
			return _scriptProvider.Get(scriptName);
		}

		public string Get(string scriptName, params object[] parameters)
		{
			return _scriptProvider.Get(scriptName, parameters);
		}
	}
}