using Newtonsoft.Json;

namespace VGen.Backend.Audio.Model;

public class User
{
    [JsonProperty("id")]
    public string Id { get; set; }
    [JsonProperty("email")]
    public string Email { get; set; }
    [JsonProperty("username")]
    public string Username { get; set; }
    [JsonProperty("limited")]
    public bool Limited { get; set; }
    [JsonProperty("trialCount")]
    public int TrialCount { get; set; }
    [JsonProperty("isBanned")]
    public bool IsBanned { get; set; }
    [JsonProperty("lastLoginDate")]
    public string LastLoginDate { get; set; }
    [JsonProperty("creationDate")]
    public string CreationDate { get; set; }
    [JsonProperty("authProvider")]
    public string AuthProvider { get; set; }
    [JsonProperty("role")]
    public string Role { get; set; }
}