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
        [FunctionName("QueueTriggerFunction")]
        public static async Task Run([QueueTrigger("deployimage", Connection = "AzureWebJobsStorage")] string message,
            ILogger log,
            [DurableClient] IDurableOrchestrationClient starter)
        {
            log.LogInformation($"Message Received: {message}");

            var args = JsonConvert.DeserializeObject<StartImageParams>(message);

            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("DeployImageToAci", args);

        }
    }
}
