using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ContainerInstance.Fluent;
using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DurableOne
{
    public static class DeployerFunction
    {

        /// <summary>
        /// Deploys the container to ACI.
        /// It triggers the azure function function 'DeployImageToAciTask' (durable task) that starts deploying process to ACI.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("DeployContainerOrchestration")]
        public static async Task<string> DeployContainerOrchestration([OrchestrationTrigger]
        IDurableOrchestrationContext context, ILogger log)
        {
            StartImageParams startParams = context.GetInput<StartImageParams>();

            log.LogInformation($"{nameof(DeployContainerOrchestration)} started with TransactionId: {startParams.TransactionId} and InstanceId: {context.InstanceId}");

            // Runs the deployment activity.
            var output = await context.CallActivityAsync<string>("DeployImageToAciTask", startParams);

            return output;
        }

        /// <summary>
        /// Creates a container group with a single container asynchronously, and
        /// polls its status until its state is 'Running'.
        /// </summary>
        /// <param name="azure">An authenticated IAzure object.</param>
        /// <param name="resourceGroupName">The name of the resource group in which to create the container group.</param>
        /// <param name="containerGroupName">The name of the container group to create.</param>
        /// <param name="containerImage">The container image name and tag, for example 'microsoft\aci-helloworld:latest'.</param>
        /// <remarks>
        /// https://github.com/Azure-Samples/aci-docs-sample-dotnet/blob/master/Program.
        /// </remarks>
        [FunctionName("DeployImageToAciTask")]
        public static string DeployImageToAciTask([ActivityTrigger] StartImageParams args, ILogger logger)
        {
            logger.LogInformation($"Creating container group '{args.Arg1}'...");

            IAzure azure = GetEnvironment(args);

            // Get the resource group's region
            IResourceGroup resGroup = azure.ResourceGroups.GetByName(args.ResourceGroupName);

            Region azureRegion = resGroup.Region;

            Task deployTask = null;

            var containerGroup = azure.ContainerGroups.GetByResourceGroup(args.ResourceGroupName, args.ContainerGroupName);
            if (containerGroup == null || (containerGroup != null && containerGroup.State != "Running"))
            {
                logger.LogInformation($"{args.ContainerGroupName} - Start deploying...");

                // Create the container group using a fire-and-forget task
                deployTask = Task.Run(() =>

                   azure.ContainerGroups.Define(args.ContainerGroupName)
                       .WithRegion(azureRegion)
                       .WithExistingResourceGroup(args.ResourceGroupName)
                       .WithLinux()
                       .WithPrivateImageRegistry(args.RegistryServer, args.RegistryUsername, args.RegistryPassword)

                       //.WithNewAzureFileShareVolume("valumeshare", "fileshare")
                       .WithoutVolume()

                       .DefineContainerInstance($"{args.ContainerGroupName}")
                           .WithImage(args.ContainerImage)
                           .WithInternalTcpPort(80)
                           .WithCpuCoreCount(args.CpuCoreCount)
                           .WithMemorySizeInGB(args.MemorySizeInGB)
                           //.WithVolumeMountSetting("valumeshare", "/cpdm/logs")
                           .WithEnvironmentVariable("Arg1", args.Arg1)
                           .WithEnvironmentVariable("Arg2", args.Arg2)
                           .Attach()
                       //.WithDnsPrefix(args.ContainerGroupName)
                       .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
                       .CreateAsync()
               );
            }
            else
                logger.LogInformation($"{args.ContainerGroupName} - State: {containerGroup.State}");

            var waitTask = Task.Run(() =>
            {
                //
                // Here we are waiting on container to be deployed into ACI.
                IContainerGroup containerGroup = null;
                while ((containerGroup == null && deployTask?.Exception == null) || (containerGroup != null && containerGroup.State == null))
                {
                    containerGroup = azure.ContainerGroups.GetByResourceGroup(args.ResourceGroupName, args.ContainerGroupName);

                    logger.LogInformation($"tId: {args.TransactionId} - {args.ContainerGroupName} - Deploying...");

                    SdkContext.DelayProvider.Delay(1000);
                }

                //
                // Here we check if some error ocurred during deployment.
                if (deployTask?.Exception != null)
                {
                    throw deployTask?.Exception;
                }

                //
                // This will be entered if the same instance was already deployed, before we started depployment again.
                // In that case we need to restart the group.
                if (containerGroup?.State == "Succeeded" || containerGroup?.State == "Stopped")
                {
                    logger.LogInformation("Restarting the container group");

                    ContainerGroupsOperationsExtensions.StartAsync(containerGroup.Manager.Inner.ContainerGroups,
                                               containerGroup.ResourceGroupName,
                                               containerGroup.Name).Wait();

                    containerGroup = azure.ContainerGroups.GetByResourceGroup(args.ResourceGroupName, args.ContainerGroupName);

                    logger.LogInformation("The container group restarted");                    
                }

                //
                // Here we waiting on the container to complete running state.
                while (containerGroup?.State == "Pending" || containerGroup?.State == "Running" && deployTask?.Exception == null)
                {
                    logger.LogInformation($"tId: {args.TransactionId} - {args.ContainerGroupName} state: {containerGroup.Refresh().State}");

                    Thread.Sleep(15000);
                }

                string logContent = GetLogContent(containerGroup, args.ContainerGroupName);
                logger.LogInformation(logContent);

                if (deployTask?.Exception != null)
                {
                    throw deployTask?.Exception;
                }
                else if (containerGroup.Containers[args.ContainerGroupName]?.InstanceView?.CurrentState.DetailStatus == "Error" ||
                        containerGroup.State == "Failed")
                {
                    throw new Exception($"The container tId: {args.TransactionId} - {args.ContainerGroupName} has failed.");
                }
                else
                {
                    logger.LogInformation($"tId: {args.TransactionId} - {args.ContainerGroupName} state: {containerGroup.Inner.InstanceView.State}");
                    return containerGroup.State;
                }
            });

            waitTask.Wait();

            if (waitTask.Exception != null)
                throw waitTask.Exception;

            return "This should never be returned.";
        }

        public static IAzure GetEnvironment(StartImageParams args)
        {
            ServicePrincipalLoginInformation principal = new ServicePrincipalLoginInformation()
            {
                ClientId = args.ClientId,
                ClientSecret = args.Secret,
            };

            AzureCredentials azureCredential = new AzureCredentials(principal, args.TenantId, AzureEnvironment.AzureGlobalCloud);
            IAzure azure = Microsoft.Azure.Management.Fluent.Azure.Authenticate(azureCredential).WithSubscription(args.SubscriptionId);

            return azure;
        }

        /// <summary>
        /// If the container has completed too fast (<1 Min) then the log is not available.
        /// In this case we need to retry.
        /// </summary>
        /// <param name="grp"></param>
        /// <param name="containerGroupName"></param>
        /// <returns></returns>
        private static string GetLogContent(IContainerGroup grp, string containerGroupName)
        {
            Exception ex = null;
            int n = 100;
            while (--n > 0)
            {
                try
                {
                    var log = grp.GetLogContent(containerGroupName);
                    return log;
                }
                catch (Microsoft.Rest.Azure.CloudException cex)
                {
                    ex = cex;
                    Thread.Sleep(1000);
                }
            }

            throw ex;
        }
    }
}