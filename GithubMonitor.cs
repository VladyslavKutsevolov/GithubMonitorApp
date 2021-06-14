using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace GithubMonitorApp
{
    public static class GithubMonitor
    {
        [FunctionName("ProcessGithubResponse")]
        public static async Task RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var githubCommits = context.GetInput<dynamic>();
            await context.CallActivityAsync<string>("PostToSlack", githubCommits);
            
        }
        
        [FunctionName("PostToSlack")]
        public static async Task<string> PostToSlack([ActivityTrigger] IDurableActivityContext context, ILogger log)
        {
            var data = context.GetInput<dynamic>();
            
        
            using (var client = new HttpClient())
            {
                string message =
                    $"{data.commits[0].author.name} pushed to {data.repository.name} and changed {data.commits[0].modified[0]} with message: {data.commits[0].message}";
                var body = new StringContent("{'text': '"+ message + "' }", Encoding.UTF8, "application/json");
                log.LogInformation($"body: {body.ReadAsStringAsync().Result}");
                var response =
                    await client.PostAsync(
                        Environment.GetEnvironmentVariable("SLACK_WEB_HOOK"), body);
        
                var result = await response.Content.ReadAsStringAsync();
                
                log.LogInformation($"result: {result}");
        
                return result;
            }
            
        
        }
        
        [FunctionName("GitHubMonitor")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")]
            HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            // Function input comes from the request content.
            dynamic data = await req.Content.ReadAsAsync<dynamic>();
            string instanceId = await starter.StartNewAsync("ProcessGithubResponse", null, data);
    
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
    
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}