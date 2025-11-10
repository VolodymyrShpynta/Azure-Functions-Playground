using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask.Http;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace My.Function
{
    /// <summary>
    /// Result we return from the orchestration.
    /// Keep it simple: HTTP status and response body.
    /// </summary>
    public record FixedHttpResult(int StatusCode, string? Content);

    public static class FetchFixedHttp
    {
        // Hardcoded endpoint (change to the one you need)
        private static readonly Uri Target = new("https://www.google.com/");

        /// <summary>
        /// Orchestration that calls a fixed external HTTP endpoint using Durable HTTP.
        /// - Uses a deterministic retry loop (3 attempts, exponential backoff).
        /// - No DurableHttpRequestOptions (works with 1.x packages).
        /// </summary>
        [Function(nameof(FetchFixedHttp))]
        public static async Task<FixedHttpResult> Run(
            [OrchestrationTrigger] TaskOrchestrationContext context)
        {
            var logger = context.CreateReplaySafeLogger(nameof(FetchFixedHttp));
            logger.LogInformation("FetchFixedHttp orchestration starting. Target = {Url}", Target);

            // Build request (add headers/content if you need POST/PUT/PATCH)
            var request = new DurableHttpRequest(
                method: HttpMethod.Get,
                uri: Target
            // headers: new Dictionary<string, string> { { "Accept", "text/html" } },
            // content: "{\"hello\":\"world\"}",
            // contentType: "application/json"
            );

            // Deterministic retry: 3 attempts, exponential backoff starting at 5s
            int attempts = 0;
            const int maxAttempts = 3;
            TimeSpan backoff = TimeSpan.FromSeconds(5);

            while (true)
            {
                attempts++;
                logger.LogInformation("Attempt {Attempt} calling {Url}", attempts, Target);

                DurableHttpResponse resp = await context.CallHttpAsync(request);

                int status = (int)resp.StatusCode;
                bool isSuccess = status >= 200 && status < 300;

                if (isSuccess)
                {
                    logger.LogInformation("HTTP call succeeded with {Status}", status);
                    return new FixedHttpResult(status, resp.Content);
                }

                logger.LogWarning("HTTP call returned {Status}.", status);

                if (attempts >= maxAttempts)
                {
                    logger.LogWarning("Max attempts reached. Returning last response.");
                    return new FixedHttpResult(status, resp.Content);
                }

                // Deterministic delay (replay-safe)
                var fireAt = context.CurrentUtcDateTime.Add(backoff);
                await context.CreateTimer(fireAt, CancellationToken.None);

                // Exponential backoff
                backoff = TimeSpan.FromSeconds(backoff.TotalSeconds * 2);
            }
        }

        /// <summary>
        /// Starter that returns 202 + status URLs (standard Durable pattern).
        /// Invoke: GET/POST /api/FetchFixedHttp_HttpStart
        /// </summary>
        [Function("FetchFixedHttp_HttpStart")]
        public static async Task<HttpResponseData> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(HttpStart));

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(FetchFixedHttp));
            logger.LogInformation("Started FetchFixedHttp orchestration. InstanceId = {InstanceId}", instanceId);

            // Returns 202 with statusQueryGetUri, sendEventPostUri, etc.
            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }

        /// <summary>
        /// Starter that waits up to 60s for completion; if it finishes in time,
        /// returns 200 + orchestration output. Otherwise, falls back to 202 + status URLs.
        /// Invoke: GET/POST /api/FetchFixedHttp_HttpStart_Wait
        /// </summary>
        [Function("FetchFixedHttp_HttpStart_Wait")]
        public static async Task<HttpResponseData> HttpStartWait(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger(nameof(HttpStartWait));

            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(nameof(FetchFixedHttp));
            logger.LogInformation("Started FetchFixedHttp (wait). InstanceId = {InstanceId}", instanceId);

            // Wait up to 60 seconds, then automatically fall back to 202 if not done
            var timeout = TimeSpan.FromSeconds(60);
            return await client.WaitForCompletionOrCreateCheckStatusResponseAsync(req, instanceId, timeout);
        }
    }
}
