﻿using System.Diagnostics.CodeAnalysis;
using EShopworld.WorkerProcess.CosmosDistributedLock;
using Microsoft.Azure.Documents;

namespace EShopworld.WorkerProcess.Configuration
{
    /// <summary>
    /// The <see cref="CosmosDataStoreOptions"/> class
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class CosmosDataStoreOptions
    {
        /// <summary>
        /// The database
        /// </summary>
        public string Database { get; set; } = "WorkerProcess";

        /// <summary>
        /// The document collection for leases
        /// </summary>
        public string LeasesCollection { get; set; } = "WorkerLeases";
        
        /// <summary>
        /// The document collection for leases request
        /// </summary>
        public string RequestsCollection { get; set; } = "LeaseRequests";

        /// <summary>
        /// The document collection for storing distributed lock. Only required for <see cref="DistributedLock"/>.
        /// </summary>
        public string DistributedLocksCollection { get; set; } = "DistributedLocks";

        /// <summary>
        /// The consistency level for the store
        /// </summary>
        public ConsistencyLevel ConsistencyLevel { get; set; } = ConsistencyLevel.Strong;

        /// <summary>
        /// The offer through put used when creating the collection
        /// </summary>
        public int OfferThroughput { get; set; } = 400;

        /// <summary>
        /// CosmosDb Connection String
        /// </summary>
        public string ConnectionString { get; set; }
    }
}
