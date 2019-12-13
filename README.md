# Introduction

The EShopworld.WorkerProcess contains classes for the management of worker process interactions

## Installation

Get the latest version of the package from <https://>

```ps
Install-Package EShopworld.WorkerProcess 
```

## IWorkerLease

The IWorkerLease component allows distinct instances of IWorkerLease components to cooperatively acquire a mutual exclusion lease for a period of time.

### Usage

To use the WorkerProcess component the first step is to add the various components to the Di container. Note that this step depends on your DI framework and your hosting environment.

In the public void ConfigureServices(IServiceCollection services) method include the following lines:

```csharp
    // Include the worker process services
    services.AddWorkerLease();
```

NOTE: The above creates a singleton instance of the IWorkerLease component. This is fine for a single executing process but will not give the correct behavior when multiple IWorkerLease instances are required for managing separate worker threads.

### Initialise default ILeaseStore

The default ILeaseStore is based on CosmosDb document database. This store needs to be initialised once off to create the database and collection to store the worker process leases.

```csharp
    // Initalise the lease store instance
    await leaseStore.InitialiseAsync();
```

### Configuration Options

Configuration Setting | Description | Default
---------|----------|----------
