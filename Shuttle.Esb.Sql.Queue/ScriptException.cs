using System;

namespace Shuttle.Esb.Sql.Queue
{
	public class ScriptException : Exception
	{
		public ScriptException(string message)
			: base(message)
		{
		}
	}
}