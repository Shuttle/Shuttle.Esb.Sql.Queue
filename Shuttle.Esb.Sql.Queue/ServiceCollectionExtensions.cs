using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.Sql.Queue;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqlQueue(this IServiceCollection services, Action<SqlQueueBuilder>? builder = null)
    {
        var sqlQueueBuilder = new SqlQueueBuilder(Guard.AgainstNull(services));

        builder?.Invoke(sqlQueueBuilder);

        services.AddSingleton<IValidateOptions<SqlQueueOptions>, SqlQueueOptionsValidator>();

        foreach (var pair in sqlQueueBuilder.SqlQueueOptions)
        {
            services.AddOptions<SqlQueueOptions>(pair.Key).Configure(options =>
            {
                options.ConnectionStringName = pair.Value.ConnectionStringName;
                options.Schema = pair.Value.Schema;
            });
        }

        services.AddSingleton<IQueueFactory, SqlQueueFactory>();

        return services;
    }
}