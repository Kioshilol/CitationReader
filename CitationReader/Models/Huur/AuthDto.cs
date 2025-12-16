using System.Text.Json.Serialization;

namespace CitationReader.Models
{
    public class AuthDto
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("tokenExpired")]
        public DateTime TokenExpired { get; set; }
    }
}
