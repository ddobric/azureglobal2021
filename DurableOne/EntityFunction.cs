using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DurableOne
{
    public static class EntityFunction
    {
        //[FunctionName("Counter")]
        //public static void Counter([EntityTrigger] IDurableEntityContext ctx)
        //{
        //    switch (ctx.OperationName.ToLowerInvariant())
        //    {
        //        case "add":
        //            ctx.SetState(ctx.GetState<int>() + ctx.GetInput<int>());
        //            break;
        //        case "reset":
        //            ctx.SetState(0);
        //            break;
        //        case "get":
        //            ctx.Return(ctx.GetState<int>());
        //            break;
        //    }
        //}


        [JsonObject(MemberSerialization.OptIn)]
        public class Counter
        {
            [JsonProperty("value")]
            public int Value { get; set; }

            public void Add(int amount)
            {
                this.Value += amount;
            }

            public Task Reset()
            {
                this.Value = 0;
                return Task.CompletedTask;
            }

            public Task<int> Get()
            {
                return Task.FromResult(this.Value);
            }

            public void Delete()
            {
                Entity.Current.DeleteState();
            }
        }

        [FunctionName(nameof(Counter))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx)
              => ctx.DispatchAsync<Counter>();

        [FunctionName("CounterEntity")]
        public static async Task<HttpResponseMessage> CounterEntity(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "Counter/{method}/{key}/{input}")] HttpRequestMessage req,
           [DurableClient] IDurableEntityClient client, ILogger log, string method = "get", string key = null, string input = null
           )
        {
            var entityId = new EntityId(nameof(Counter), key);

            HttpResponseMessage msg = new HttpResponseMessage();

            try
            {
                if (method == "add")
                {
                    int amount = int.Parse(input);
                    await client.SignalEntityAsync(entityId, nameof(Counter.Add), amount);
                }
                else if (method == "reset")
                    await client.SignalEntityAsync(entityId, method);
                else
                {
                    var resp = await client.ReadEntityStateAsync<Counter>(entityId);

                    msg.Content = new StringContent($"{entityId}, State={resp.EntityState?.Value}");
                }

                msg.StatusCode = System.Net.HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                msg.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                msg.Content = new StringContent($"{ex}");
            }

            return msg;
        }
    }
}
