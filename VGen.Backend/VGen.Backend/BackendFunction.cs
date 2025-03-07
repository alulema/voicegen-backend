using System.Text;
using System.Text.Json;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using VGen.Backend.Model;
using User = VGen.Backend.Model.User;

namespace VGen.Backend;

public class BackendFunction
{
    private readonly ILogger<BackendFunction> _logger;
    private readonly Container _localContainer;
    private readonly CosmosClient _localCosmosClient;
    private static readonly HttpClient LocalHttpClient = new();
    private static readonly string? OpenAiApiKey = Environment.GetEnvironmentVariable("OpenAIApiKey");
    private static readonly string? EndpointUrl = Environment.GetEnvironmentVariable("CosmosDBEndpoint");
    private static readonly string? PrimaryKey = Environment.GetEnvironmentVariable("CosmosDBPrimaryKey");
    private static readonly string? DatabaseId = Environment.GetEnvironmentVariable("DatabaseId");
    private static readonly string? ContainerId = Environment.GetEnvironmentVariable("ContainerId");
    private static readonly int TrialMaximum = Convert.ToInt32(Environment.GetEnvironmentVariable("TrialMaximum"));

    public BackendFunction(ILogger<BackendFunction> logger)
    {
        _localCosmosClient = new(EndpointUrl, PrimaryKey, new CosmosClientOptions { ApplicationName = "VoiceGeneratorApp"});
        Database localDatabase = _localCosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId).GetAwaiter().GetResult();
        _localContainer = localDatabase.CreateContainerIfNotExistsAsync(ContainerId, "/email").GetAwaiter().GetResult();

        _logger = logger;
    }

    [Function("VGenBackendGenerateSpeech")]
    public async Task<IActionResult> RunGenerateSpeech(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "generate-speech")] HttpRequest req)
    {
        string email;
        _logger.LogInformation("GenerateSpeechFunction triggered.");
            
        // Read request body
        var bearerToken = req.Headers["Authorization"];
        var token = bearerToken.FirstOrDefault()?.Split(" ").Last();

        try
        {
            var data = await GoogleJsonWebSignature.ValidateAsync(token,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")],
                });
            email = data.Email;
        }
        catch (Exception e)
        {
            return new UnauthorizedObjectResult(new { Message = "Invalid token.", Error = e.Message});
        }

            
        // Read request body
        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<SpeechRequest>(requestBody);

        if (request == null || string.IsNullOrEmpty(request.Input) ||
            string.IsNullOrEmpty(request.Voice) || string.IsNullOrEmpty(request.Model))
        {
            return new BadRequestObjectResult("Invalid request parameters.");
        }
            
        // Validate user usage
        var user = await GetUserAsync(email);
        if (user == null)
        {
            return new UnauthorizedResult();
        }
            
        if (user.IsBanned)
        {
            return new ForbidResult();
        }

        if (user.Limited && user.TrialCount >= TrialMaximum)
        {
            return new BadRequestObjectResult("Trial limit reached.");
        }
            
        // Prepare OpenAI API call
        var openAiUrl = "https://api.openai.com/v1/audio/speech";
        var payload = new
        {
            model = request.Model,
            input = request.Input,
            voice = request.Voice
        };
            
        var reqOpenAi = new HttpRequestMessage(HttpMethod.Post, openAiUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        // Set the Authorization header here (not on the content)
        reqOpenAi.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", OpenAiApiKey);
            
        // Call OpenAI API
        var openAiResponse = await LocalHttpClient.SendAsync(reqOpenAi);
            
        if (!openAiResponse.IsSuccessStatusCode)
        {
            _logger.LogError($"OpenAI API error: {openAiResponse.StatusCode}");
            return new StatusCodeResult((int)openAiResponse.StatusCode);
        }

        // Increment trialCount for limited users
        if (user is { Limited: true, TrialCount: < 3 })
        {
            user.TrialCount++;
            await _localContainer.UpsertItemAsync(user, new PartitionKey(user.Email));
        }

        // Stream the audio back to Angular
        var audioStream = await openAiResponse.Content.ReadAsStreamAsync();
        var memoryStream = new MemoryStream();
        await audioStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return new FileStreamResult(memoryStream, "audio/mpeg")
        {
            FileDownloadName = "generated_speech.mp3"
        };
    }
        
    [Function("VGenBackendAuth")]
    public async Task<IActionResult> RunAuth(
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
            var user = await _localContainer.ReadItemAsync<User>(email, new PartitionKey(email));

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
            var newUser = new User
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
        
    private async Task<User?> GetUserAsync(string email)
    {
        try
        {
            var response = await _localContainer.ReadItemAsync<User>(email, new PartitionKey(email));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}