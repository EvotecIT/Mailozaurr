using System.Text.Json.Serialization;
namespace Mailozaurr;

public class GraphInternetMessageHeader
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }
}

