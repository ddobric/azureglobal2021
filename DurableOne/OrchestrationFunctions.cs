using System;
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
        public static int Counter { get; set; } = 0;

        [FunctionName("OrchestrationWithSequence")]
        public static async Task<List<string>> OrchestrationWithSequence(
              [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            int counter = 0;

            var input = context.GetInput<string>();

            var outputs = new List<string>();

            // Replace "hello" with the name of your Durable Activity Function.
            outputs.Add(await context.CallActivityAsync<string>("ExecuteJob", "Frankfurt"));

            counter++;

            outputs.Add(await context.CallActivityAsync<string>("ExecuteJob", "Seattle"));

            counter++;

            outputs.Add(await context.CallActivityAsync<string>("ExecuteJob", "Sarajevo"));

            return outputs;
        }

        [FunctionName("OrchestrationWithFanOut")]
        public static async Task<List<string>> OrchestrationWithFanOut(
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

        #region Looping Orchestration

        [FunctionName("OrchestrationLoop")]
        public static async Task<string> OrchestrationLoop(
       [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var state = context.GetInput<State>();

            var outputs = new List<string>();

            var result = await context.CallActivityAsync<string>("LongRunningJob", state.Delay * 1000);
            
            context.SetCustomStatus(state.Counter.ToString());

            Console.WriteLine($"Counter: {state.Counter}");

            context.ContinueAsNew(new State { Delay = state.Delay, Counter = state.Counter+1 });

            return result;
        }


        [FunctionName("LongRunningJob")]
        public static async Task<string> LongRunningJob([ActivityTrigger] int delaySec, ILogger log)
        {
            log.LogInformation($"Started: {delaySec}.");

            await Task.Delay(delaySec);

            log.LogInformation($"Completed: {delaySec}.");

            return $"Hello {delaySec}!";
        }

        public class State
        {
            public int Delay { get; set; }

            public int Counter { get; set; }
        }
        #endregion


        [FunctionName("RunOrchestrationFunction")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "{pattern}")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter, string pattern,
            ILogger log)
        {
            string instanceId;

            if (pattern == "fanout")
                instanceId = await starter.StartNewAsync<string>("OrchestrationWithFanOut", "42");
            if (pattern == "loop")
                instanceId = await starter.StartNewAsync<State>("OrchestrationLoop", new State {  Counter = 0, Delay = 5});
            else
                instanceId = await starter.StartNewAsync<string>("OrchestrationWithSequence", "42");

            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}