using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.Sql.Queue
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqlQueue(this IServiceCollection services)
        {
            Guard.AgainstNull(services, nameof(services));

            services.TryAddSingleton<IScriptProvider, ScriptProvider>();
            services.TryAddSingleton<IQueueFactory, SqlQueueFactory>();

            return services;
        }

    }
}