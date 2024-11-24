using System;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;
using Shuttle.Core.Data;
using Shuttle.Core.Threading;

namespace Shuttle.Esb.Sql.Queue;

public class SqlQueueFactory : IQueueFactory
{
    private readonly IQueryFactory _queryFactory;
    private readonly ICancellationTokenSource _cancellationTokenSource;
    private readonly IDatabaseContextFactory _databaseContextFactory;
    private readonly IOptionsMonitor<SqlQueueOptions> _sqlQueueOptions;

    public SqlQueueFactory(IOptionsMonitor<SqlQueueOptions> sqlQueueOptions, IDatabaseContextFactory databaseContextFactory, IQueryFactory queryFactory, ICancellationTokenSource cancellationTokenSource)
    {
        _sqlQueueOptions = Guard.AgainstNull(sqlQueueOptions);
        _queryFactory = Guard.AgainstNull(queryFactory);
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

        return new SqlQueue(queueUri, sqlQueueOptions, _databaseContextFactory, _queryFactory, _cancellationTokenSource.Get().Token);
    }
}