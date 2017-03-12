# Shuttle.Esb.Sql.Queue

Sql RDBS implementation of the `IQueue` interface for use with Shuttl.Esb.

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
