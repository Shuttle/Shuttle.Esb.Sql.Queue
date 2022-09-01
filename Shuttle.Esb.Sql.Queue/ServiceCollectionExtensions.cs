using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.Sql.Queue
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlQueue(this IServiceCollection services, Action<SqlQueueBuilder> builder = null)
        {
            Guard.AgainstNull(services, nameof(services));

            var sqlQueueBuilder = new SqlQueueBuilder(services);

            builder?.Invoke(sqlQueueBuilder);

            services.AddSingleton<IValidateOptions<SqlQueueOptions>, SqlQueueOptionsValidator>();

            foreach (var pair in sqlQueueBuilder.SqlQueueOptions)
            {
                services.AddOptions<SqlQueueOptions>(pair.Key).Configure(options =>
                {
                    options.ConnectionStringName = pair.Value.ConnectionStringName;
                });
            }

            services.TryAddSingleton<IScriptProvider, ScriptProvider>();
            services.AddSingleton<IQueueFactory, SqlQueueFactory>();

            return services;
        }
    }
}