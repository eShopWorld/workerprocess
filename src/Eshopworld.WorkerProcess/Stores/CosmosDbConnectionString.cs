using System;
using System.Data.Common;

namespace EShopworld.WorkerProcess.Stores
{
    internal class CosmosDbConnectionString
    {
        public CosmosDbConnectionString(string connectionString)
        {
            // Use this generic builder to parse the connection string
            var builder = new DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            if (builder.TryGetValue("AccountKey", out var key))
            {
                AuthKey = key.ToString();
            }

            if (builder.TryGetValue("AccountEndpoint", out var uri))
            {
                ServiceEndpoint = new Uri(uri.ToString());
            }
        }

        public Uri ServiceEndpoint { get; set; }

        public string AuthKey { get; set; }
    }
}
