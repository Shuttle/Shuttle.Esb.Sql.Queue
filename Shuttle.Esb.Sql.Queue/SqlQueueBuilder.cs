using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.Sql.Queue;

public class SqlQueueBuilder
{
    internal readonly Dictionary<string, SqlQueueOptions> SqlQueueOptions = new();

    public SqlQueueBuilder(IServiceCollection services)
    {
        Services = Guard.AgainstNull(services);
    }

    public IServiceCollection Services { get; }

    public SqlQueueBuilder AddOptions(string name, SqlQueueOptions sqlQueueOptions)
    {
        Guard.AgainstNullOrEmptyString(name);
        Guard.AgainstNull(sqlQueueOptions);

        SqlQueueOptions.Remove(name);

        SqlQueueOptions.Add(name, sqlQueueOptions);

        return this;
    }
    public SqlQueueBuilder UseSqlServer()
    {
        Services.AddSingleton<IQueryFactory, SqlServer.QueryFactory>();

        return this;
    }
}