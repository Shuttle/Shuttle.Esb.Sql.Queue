# Shuttle.Esb.Sql.Queue

Sql RDBMS implementation of the `IQueue` interface for use with Shuttle.Esb.

# Registration

The required components may be registered by calling `ComponentRegistryExtensions.RegisterSqlQueue(IComponentRegistry)`.

# Supported providers

Currently only the `System.Data.SqlClient` provider name is supported but this can easily be extended.  Feel free to give it a bash and please send a pull request if you *do* go this route.  You are welcome to create an issue and assistance will be provided where able.

# SqlQueue

There is a `IQueue` implementation for Sql Server that enables a table-based queue.  Since this a table-based queue is not a real queuing technology it is prudent to make use of a local outbox.

## Configuration

The queue configuration is part of the specified uri, e.g.:

``` xml
    <inbox
      workQueueUri="sql://connectionstring-name/table-queue"
	  .
	  .
	  .
    />
```
