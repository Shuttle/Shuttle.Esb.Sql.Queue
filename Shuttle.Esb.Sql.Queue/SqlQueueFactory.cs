using System;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;
using Shuttle.Core.Threading;

namespace Shuttle.Esb.Sql.Queue;

public class SqlQueueFactory : IQueueFactory
{
    private readonly ICancellationTokenSource _cancellationTokenSource;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly IScriptProvider _scriptProvider;
    private readonly IOptionsMonitor<SqlQueueOptions> _sqlQueueOptions;

    public SqlQueueFactory(IOptionsMonitor<SqlQueueOptions> sqlQueueOptions, IScriptProvider scriptProvider, IDatabaseContextFactory databaseContextFactory, ICancellationTokenSource cancellationTokenSource)
    {
        _sqlQueueOptions = Guard.AgainstNull(sqlQueueOptions);
        _scriptProvider = Guard.AgainstNull(scriptProvider);
        _databaseContextFactory = Guard.AgainstNull(databaseContextFactory);
        _cancellationTokenSource = Guard.AgainstNull(cancellationTokenSource);
    }

    public string Scheme => "sql";

    public IQueue Create(Uri uri)
    {
        var queueUri = new QueueUri(Guard.AgainstNull(uri)).SchemeInvariant(Scheme);
        var sqlQueueOptions = _sqlQueueOptions.Get(queueUri.ConfigurationName);

        if (sqlQueueOptions == null)
        {
            throw new InvalidOperationException(string.Format(Esb.Resources.QueueConfigurationNameException, queueUri.ConfigurationName));
        }

        return new SqlQueue(queueUri, sqlQueueOptions, _scriptProvider, _databaseContextFactory, _cancellationTokenSource.Get().Token);
    }
}