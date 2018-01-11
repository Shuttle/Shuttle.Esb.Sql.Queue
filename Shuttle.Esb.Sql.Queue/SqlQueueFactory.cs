using System;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue
{
    public class SqlQueueFactory : IQueueFactory
    {
        private readonly IDatabaseContextFactory _databaseContextFactory;
        private readonly IDatabaseGateway _databaseGateway;
        private readonly IScriptProvider _scriptProvider;

        public SqlQueueFactory(IScriptProvider scriptProvider, IDatabaseContextFactory databaseContextFactory,
            IDatabaseGateway databaseGateway)
        {
            _scriptProvider = scriptProvider;
            _databaseContextFactory = databaseContextFactory;
            _databaseGateway = databaseGateway;
        }

        public string Scheme => SqlUriParser.Scheme;

        public IQueue Create(Uri uri)
        {
            Guard.AgainstNull(uri, "uri");

            return new SqlQueue(uri, _scriptProvider, _databaseContextFactory, _databaseGateway);
        }

        public bool CanCreate(Uri uri)
        {
            Guard.AgainstNull(uri, "uri");

            return Scheme.Equals(uri.Scheme, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}