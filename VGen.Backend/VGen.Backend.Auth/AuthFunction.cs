using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace VGen.Backend.Auth;

public class AuthFunction
{
    private readonly ILogger<AuthFunction> _logger;
    private readonly Container _localContainer;
    private readonly CosmosClient _localCosmosClient;
    private static readonly string? EndpointUrl = Environment.GetEnvironmentVariable("CosmosDBEndpoint");
    private static readonly string? PrimaryKey = Environment.GetEnvironmentVariable("CosmosDBPrimaryKey");
    private static readonly string? DatabaseId = Environment.GetEnvironmentVariable("DatabaseId");
    private static readonly string? ContainerId = Environment.GetEnvironmentVariable("ContainerId");

    public AuthFunction(ILogger<AuthFunction> logger)
    {
        _localCosmosClient = new(EndpointUrl, PrimaryKey, new CosmosClientOptions { ApplicationName = "VoiceGeneratorApp"});
        Database localDatabase = _localCosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId).GetAwaiter().GetResult();
        _localContainer = localDatabase.CreateContainerIfNotExistsAsync(ContainerId, "/email").GetAwaiter().GetResult();

        _logger = logger;
    }

    [Function("VGenBackendAuth")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "auth")] HttpRequest req)
    {
        string email;
        string username;
        _logger.LogInformation("Auth Function triggered.");
            
        // Read request body
        var bearerToken = req.Headers["Authorization"];
        var token = bearerToken.FirstOrDefault()?.Split(" ").Last();

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(token,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")],
                });
            email = payload.Email;
            username = payload.Name;
        }
        catch (Exception e)
        {
            return new UnauthorizedObjectResult(new { Message = "Invalid token.", Error = e.Message});
        }
        

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username))
        {
            return new BadRequestObjectResult("Invalid request payload.");
        }

        try
        {
            // Check if user exists
            var user = await _localContainer.ReadItemAsync<Model.User>(email, new PartitionKey(email));

            if (user != null)
            {
                // Update lastLoginDate
                user.Resource.LastLoginDate = DateTime.UtcNow.ToString("o");
                await _localContainer.UpsertItemAsync(user.Resource, new PartitionKey(email));
                _logger.LogInformation($"Update login date for user {email}.");
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // User doesn't exist, create new user
            var newUser = new Model.User
            {
                Id = email,
                Email = email,
                Username = username,
                Limited = true,
                TrialCount = 0,
                IsBanned = false,
                LastLoginDate = DateTime.UtcNow.ToString("o"),
                CreationDate = DateTime.UtcNow.ToString("o"),
                AuthProvider = "Google",
                Role = "user"
            };

            await _localContainer.CreateItemAsync(newUser, new PartitionKey(newUser.Email));
            _logger.LogInformation($"Created new user: {newUser.Email}");
        }

        _localCosmosClient.Dispose();
        return new OkObjectResult("Authentication handled successfully.");
    }
}