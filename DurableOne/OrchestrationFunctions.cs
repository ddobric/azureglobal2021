using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace DurableOne
{
    public static class AciDeployerFunction
    {
        [FunctionName("Function1")]
        public static async Task<List<string>> RunOrchestrator1(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("ExecuteJob", "Tokyo"));
            outputs.Add(await context.CallActivityAsync<string>("ExecuteJob", "Seattle"));
            outputs.Add(await context.CallActivityAsync<string>("ExecuteJob", "London"));

            // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
            return outputs;
        }

        [FunctionName("Function2")]
        public static async Task<List<string>> RunOrchestrator2(
          [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var tasks = new Task<string>[3];
            
            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            tasks[0] = context.CallActivityAsync<string>("ExecuteJob", "Frankfurt");
            tasks[1] = context.CallActivityAsync<string>("ExecuteJob", "Seattle");
            tasks[2] = context.CallActivityAsync<string>("ExecuteJob", "Sarajevo");

            await Task.WhenAll(tasks);

            outputs.Add(tasks[0].Result);
            outputs.Add(tasks[1].Result);
            outputs.Add(tasks[2].Result);

            return outputs;
        }


        [FunctionName("ExecuteJob")]
        public static async Task<string> ExecuteJob([ActivityTrigger] string name, ILogger log)
        {
            log.LogInformation($"Started: {name}.");

            await Task.Delay(5000);

            log.LogInformation($"Completed: {name}.");

            return $"Hello {name}!";
        }

        [FunctionName("RunOrchestrationFunction")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "{pattern}")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter, string pattern,
            ILogger log)
        {
            string instanceId;

            if (pattern == "fanout")
                instanceId = await starter.StartNewAsync("Function2", null);
            else
                instanceId = await starter.StartNewAsync("Function1", null);

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}