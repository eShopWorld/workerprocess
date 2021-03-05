using Autofac;
using Eshopworld.Core;
using EShopworld.WorkerProcess.CosmosDistributedLock;
using EShopworld.WorkerProcess.Stores;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace EShopworld.WorkerProcess.Configuration.Autofac
{
    public static class ContainerBuilderExtensions
    {
        private const string CosmosDataStoreOptions = "WorkerProcess:CosmosDataStore";
        private const string CosmosConnectionKeyVaultKey = "cm--cosmos-connection--worker-process";

        /// <summary>
        /// Adds <see cref="DistributedLock"/> to the services collection
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configuration"></param>
        public static void AddCosmosDistributedLock(this ContainerBuilder builder, IConfigurationRoot configuration)
        {
            var cosmosDataStoreOptions = Options.Create(configuration.BindSection<CosmosDataStoreOptions>(CosmosDataStoreOptions,
                m => { m.AddMapping(x => x.ConnectionString, CosmosConnectionKeyVaultKey); }));

            builder.Register(ctx => cosmosDataStoreOptions)
                .As<IOptions<CosmosDataStoreOptions>>().SingleInstance();

            var cosmosDbConnectionString = new CosmosDbConnectionString(cosmosDataStoreOptions.Value.ConnectionString);
            var documentClient = new DocumentClient(cosmosDbConnectionString.ServiceEndpoint, cosmosDbConnectionString.AuthKey);

            builder.Register(ctx =>
                new CosmosDistributedLockStore(
                    documentClient,
                    cosmosDataStoreOptions,
                    ctx.Resolve<IBigBrother>())).As<ICosmosDistributedLockStore>().SingleInstance();

            builder.RegisterType<DistributedLock>().As<IDistributedLock>();
        }
    }
}
