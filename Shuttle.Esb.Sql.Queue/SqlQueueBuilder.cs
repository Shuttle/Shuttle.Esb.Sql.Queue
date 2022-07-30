using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shuttle.Core.Contract;

namespace Shuttle.Esb.Sql.Queue
{
    public class SqlQueueBuilder
    {
        internal readonly Dictionary<string, SqlQueueOptions> SqlQueueOptions = new Dictionary<string, SqlQueueOptions>();
        public IServiceCollection Services { get; }

        public SqlQueueBuilder(IServiceCollection services)
        {
            Guard.AgainstNull(services, nameof(services));

            Services = services;
        }

        public SqlQueueBuilder AddOptions(string name, SqlQueueOptions amazonSqsOptions)
        {
            Guard.AgainstNullOrEmptyString(name, nameof(name));
            Guard.AgainstNull(amazonSqsOptions, nameof(amazonSqsOptions));

            SqlQueueOptions.Remove(name);

            SqlQueueOptions.Add(name, amazonSqsOptions);

            return this;
        }
    }
}