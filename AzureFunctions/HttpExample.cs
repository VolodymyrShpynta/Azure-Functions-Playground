using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace My.Functions;

public class HttpExample
{
    private readonly ILogger<HttpExample> _logger;

    public HttpExample(ILogger<HttpExample> logger)
    {
        _logger = logger;
    }

    [Function("HttpExample")]
    public async Task<MultiResponse> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
                                         FunctionContext executionContext)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var message = "Welcome to Azure Functions!";

        // Return a response to both HTTP trigger and Azure Cosmos DB output binding.
        return new MultiResponse()
        {
            Document = new MyDocument
            {
                Id = Guid.NewGuid().ToString(),
                Message = message
            },
            HttpResult = new OkObjectResult(message)
        };
    }

    public class MultiResponse
    {
        [CosmosDBOutput("my-cosmos-database", "my-container", Connection = "CosmosDbConnectionString", CreateIfNotExists = true)]
        public required MyDocument Document { get; set; }

        [HttpResult]
        public required IActionResult HttpResult { get; set; }
    }

    public class MyDocument
    {
        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("message")]
        public required string Message { get; set; }
    }
}
