using System;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;
using Shuttle.Core.Threading;

namespace Shuttle.Esb.Sql.Queue
{
    public class SqlQueueFactory : IQueueFactory
    {
        private readonly IDatabaseContextService _databaseContextService;
        private readonly IDatabaseContextFactory _databaseContextFactory;
        private readonly IDatabaseGateway _databaseGateway;
        private readonly ICancellationTokenSource _cancellationTokenSource;
        private readonly IOptionsMonitor<SqlQueueOptions> _sqlQueueOptions;
        private readonly IScriptProvider _scriptProvider;

        public SqlQueueFactory(IOptionsMonitor<SqlQueueOptions> sqlQueueOptions, IScriptProvider scriptProvider, IDatabaseContextService databaseContextService, IDatabaseContextFactory databaseContextFactory, IDatabaseGateway databaseGateway, ICancellationTokenSource cancellationTokenSource)
        {
            _sqlQueueOptions = Guard.AgainstNull(sqlQueueOptions, nameof(sqlQueueOptions));
            _scriptProvider = Guard.AgainstNull(scriptProvider, nameof(scriptProvider));
            _databaseContextService = Guard.AgainstNull(databaseContextService, nameof(databaseContextService));
            _databaseContextFactory = Guard.AgainstNull(databaseContextFactory, nameof(databaseContextFactory));
            _databaseGateway = Guard.AgainstNull(databaseGateway, nameof(databaseGateway));
            _cancellationTokenSource = Guard.AgainstNull(cancellationTokenSource, nameof(cancellationTokenSource));
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

            return new SqlQueue(queueUri, sqlQueueOptions, _scriptProvider, _databaseContextService, _databaseContextFactory, _databaseGateway, _cancellationTokenSource.Get().Token);
        }
    }
}