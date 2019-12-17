# Introduction

The EShopworld.WorkerProcess contains classes for the management of worker process interactions

## Installation

Get the latest version of the package from <https://>

```ps
Install-Package EShopworld.WorkerProcess 
```

## IWorkerLease

The IWorkerLease component allows distinct instances of IWorkerLease components to cooperatively acquire and release a mutual exclusion lease for a period of time.

Since there is no central lease manager each of the IWorkerLease instances must cooperate to acquire and release lease instances. Worker lease instances will schedule themselves to acquire leases at the configured Leaseinterval.

For example if the LeaseInterval is 10 minutes each of the IWorkerLease instances will schedule the first lease acquire cycle for the next whole interval with in the hour. So if the workers start the lease cycle at 12:01, each IWorkerLease will wait until 12:10 to start the lease cycle.

**NOTE:** Since the IWorkerLease instances synchronise the acquiring of leases based on the local time of the machine it is important that the clocks be syncronised via NTP. Any inaccuracy in the clocks will effect the lower bound for the LeaseInterval and or the concurrency of the lease acquisition by the various IWorkerLease instances.

### Usage

To use the WorkerProcess component the first step is to add the various components to the Di container. Note that this step depends on your DI framework and your hosting environment.

In the public void ConfigureServices(IServiceCollection services) method include the following lines:

```csharp
    // Include the worker process services
    services.AddWorkerLease();
```

NOTE: The above creates a singleton instance of the IWorkerLease component. This is fine for a single executing process but will not give the correct behavior when multiple IWorkerLease instances are required for managing separate worker threads.

```csharp
    // One off initalisation of the ILeaseStore instance
    await leaseStore.InitialiseAsync();

    // Attach the lease allocated callback
    workerLease.LeaseAllocated += (sender, args) =>
    {
        _output.WriteLine($"[{DateTime.UtcNow}] Lease allocated until [{args.Expiry}]");
        // Start doing work
    };

    workerLease.LeaseExpired += (sender, args) =>
    {
        _output.WriteLine($"[{DateTime.UtcNow}] Lease expired");
        
        // Stop doing work
    };

    // Start this worker lease instance leasing
    workerLease.StartLeasing();
```

### Initialise default ILeaseStore

The default ILeaseStore is based on CosmosDb document database. This store needs to be initialised once off to create the database and collection to store the worker process leases.

```csharp
    // Initalise the lease store instance
    await leaseStore.InitialiseAsync();
```

### Configuration Options

#### WorkerLeaseOptions

Configuration Setting | Description | Default
---------|----------|----------
WorkerType | This is a category type of a worker that acquires leases | There is no default value and the worker type should be set to a domain specific value
Priority | This is the priority of the worker process | The default is zero which is the highest priority
LeaseInterval | The amount of time in minutes that the lease is acquired | Defaults to 5 mins

##### CosmosDataStoreOptions

Configuration Setting | Description | Default
---------|----------|----------
Database | The CosmosDB database name | WorkerProcess
Collection | The cosmosDB collection name to store the lease | WorkerLeases
ConsistencyLevel | The consistency level for the store | Strong
OfferThroughput | The offer through put used when creating the collection | 400