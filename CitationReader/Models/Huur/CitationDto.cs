using System.Text.Json.Serialization;

namespace CitationReader.Models.Huur
{
  /// <summary>
/// Individual parking violation record
/// </summary>
public class CitationDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("citationNumber")]
    public string? CitationNumber { get; set; }

    [JsonPropertyName("noticeNumber")]
    public string? NoticeNumber { get; set; }

    [JsonPropertyName("provider")]
    public int Provider { get; set; }

    [JsonPropertyName("agency")]
    public string? Agency { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("issueDate")]
    public DateTime? IssueDate { get; set; }

    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("paymentStatus")]
    public int PaymentStatus { get; set; }

    [JsonPropertyName("fineType")]
    public int FineType { get; set; }

    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}
}
