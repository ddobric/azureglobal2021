using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DurableOne
{
    public static class QueueTriggerFunction
    {
        [FunctionName("ContainerOrchestratorQueueTriggerFunction")]
        public static async Task Run([QueueTrigger("deployimage", Connection = "AzureWebJobsStorage")] string message,
            ILogger log,
            [DurableClient] IDurableOrchestrationClient orchestrationClient)
        {
            log.LogInformation($"Message Received: {message}");

            var args = JsonConvert.DeserializeObject<StartImageParams>(message);

            // Runs the orchestration.
            string instanceId = await orchestrationClient.StartNewAsync("DeployContainerOrchestration", args);

        }
    }
}
