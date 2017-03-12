# Shuttle.Esb.Sql.Queue

Microsoft Sql RDBS implementation for use with Shuttl.Esb:

- `SubscriptionManager` implements `ISubscriptionManager`
- `IdempotenceService` implements `iIdempotenceService`
- `SqlQueue` implements `IQueue`

# Supported providers

In order to use the relevant provider you need to register an `IScriptProviderConfiguration` that tells the various components where to find the relevant scripts:

```
var container = new WindsorComponentContainer(new WindsorContainer());

container.Register<IScriptProviderConfiguration>(new ScriptProviderConfiguration
{
    ResourceAssembly = typeof (SqlQueue).Assembly,
    ResourceNameFormat = "Shuttle.Esb.Sql.Queue.Scripts.System.Data.SqlClient.{ScriptName}.sql"
});
```

Currently only the `System.Data.SqlClient` provider name is supported but this can easily be extended.  Feel free to give it a bash and please send a pull request if you *do* go this route.  You are welcome to create an issue and assistance will be provided where able.

# SqlQueue

There is a `IQueue` implementation for Sql Server that enables a table-based queue.  Since this a table-based queue is not a real queuing technology it is prudent to make use of a local outbox.

## Configuration

The queue configuration is part of the specified uri, e.g.:

~~~xml
    <inbox
      workQueueUri="sql://connectionstring-name/table-queue"
	  .
	  .
	  .
    />
~~~

In addition to this there is also a Sql Server specific section (defaults specified here):

~~~xml
<configuration>
  <configSections>
    <section name='sqlServer' type="Shuttle.Esb.Sql.Queue.SqlSection, Shuttle.Esb.Sql.Queue"/>
  </configSections>
  
  <sqlServer
	subscriptionManagerConnectionStringName="Subscription"
	idempotenceServiceConnectionStringName="Idempotence"
  />
  .
  .
  .
<configuration>
~~~

# SubscriptionManager

A Sql Server based `ISubscriptionManager` implementation is also provided.  The subscription manager caches all subscriptions forever so should a new subscriber be added be sure to restart the publisher endpoint service.

# IdempotenceService

A `IIdempotenceService` implementation is also available for Sql Server.