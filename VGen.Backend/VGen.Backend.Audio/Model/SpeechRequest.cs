using System.Text.Json.Serialization;

namespace VGen.Backend.Audio.Model;

public class SpeechRequest
{
    [JsonPropertyName("input")]
    public string Input { get; set; }
    [JsonPropertyName("model")]
    public string Model { get; set; }
    [JsonPropertyName("voice")]
    public string Voice { get; set; }
}