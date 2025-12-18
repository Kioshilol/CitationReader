using System.Text.Json.Serialization;

namespace CitationReader.Models.Citation
{
    public class RmcPayOperatorInfoResponse
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("data")]
        public OperatorData Data { get; set; } = new();

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    public class OperatorData
    {
        [JsonPropertyName("operators")]
        public List<Operator> Operators { get; set; } = new();

        [JsonPropertyName("search_type")]
        public int SearchType { get; set; }
    }

    public class Operator
    {
        [JsonPropertyName("ticketnumber")]
        public string TicketNumber { get; set; } = string.Empty;

        [JsonPropertyName("datecreated")]
        public string DateCreated { get; set; } = string.Empty;

        [JsonPropertyName("lpn")]
        public string Lpn { get; set; } = string.Empty;

        [JsonPropertyName("vin")]
        public string? Vin { get; set; }

        [JsonPropertyName("operator_name")]
        public string OperatorName { get; set; } = string.Empty;

        [JsonPropertyName("operator_id")]
        public string OperatorId { get; set; } = string.Empty;

        [JsonPropertyName("subdomain")]
        public string Subdomain { get; set; } = string.Empty;

        [JsonPropertyName("operator_location")]
        public string? OperatorLocation { get; set; }

        [JsonPropertyName("redirect_url")]
        public string RedirectUrl { get; set; } = string.Empty;

        [JsonPropertyName("immobilization_device_number")]
        public string? ImmobilizationDeviceNumber { get; set; }

        [JsonPropertyName("immobilization_status")]
        public string? ImmobilizationStatus { get; set; }

        [JsonPropertyName("immobilization_id")]
        public string? ImmobilizationId { get; set; }
    }
}
