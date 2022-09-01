using System;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;

namespace Shuttle.Esb.Sql.Queue
{
    public class SqlQueueFactory : IQueueFactory
    {
        private readonly IDatabaseContextFactory _databaseContextFactory;
        private readonly IDatabaseGateway _databaseGateway;
        private readonly IOptionsMonitor<SqlQueueOptions> _sqlQueueOptions;
        private readonly IScriptProvider _scriptProvider;

        public SqlQueueFactory(IOptionsMonitor<SqlQueueOptions> sqlQueueOptions,  IScriptProvider scriptProvider, IDatabaseContextFactory databaseContextFactory,
            IDatabaseGateway databaseGateway)
        {
            Guard.AgainstNull(sqlQueueOptions, nameof(sqlQueueOptions));
            Guard.AgainstNull(scriptProvider, nameof(scriptProvider));
            Guard.AgainstNull(databaseContextFactory, nameof(databaseContextFactory));
            Guard.AgainstNull(databaseGateway, nameof(databaseGateway));

            _sqlQueueOptions = sqlQueueOptions;
            _scriptProvider = scriptProvider;
            _databaseContextFactory = databaseContextFactory;
            _databaseGateway = databaseGateway;
        }

        public string Scheme => "sql";

        public IQueue Create(Uri uri)
        {
            Guard.AgainstNull(uri, nameof(uri));

            var queueUri = new QueueUri(uri).SchemeInvariant(Scheme);
            var sqlQueueOptions = _sqlQueueOptions.Get(queueUri.ConfigurationName);

            if (sqlQueueOptions == null)
            {
                throw new InvalidOperationException(string.Format(Esb.Resources.QueueConfigurationNameException, queueUri.ConfigurationName));
            }

            return new SqlQueue(queueUri, sqlQueueOptions, _scriptProvider, _databaseContextFactory, _databaseGateway);
        }
    }
}