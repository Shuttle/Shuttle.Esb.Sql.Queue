using Microsoft.Extensions.Options;

namespace Shuttle.Esb.Sql.Queue;

public class SqlQueueOptionsValidator : IValidateOptions<SqlQueueOptions>
{
    public ValidateOptionsResult Validate(string? name, SqlQueueOptions options)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return ValidateOptionsResult.Fail(Esb.Resources.QueueConfigurationNameException);
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            return ValidateOptionsResult.Fail(string.Format(Esb.Resources.QueueConfigurationItemException, name, nameof(options.ConnectionStringName)));
        }

        return ValidateOptionsResult.Success;
    }
}