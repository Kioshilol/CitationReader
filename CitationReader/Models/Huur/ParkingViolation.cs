using System.Text.Json.Serialization;

namespace CitationReader.Models.Huur;

public class ParkingViolation
{
    /// <summary>
    /// Violation ID
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Citation number
    /// </summary>
    [JsonPropertyName("citationNumber")]
    public string? CitationNumber { get; set; }

    /// <summary>
    /// Notice number
    /// </summary>
    [JsonPropertyName("noticeNumber")]
    public string? NoticeNumber { get; set; }

    /// <summary>
    /// Provider
    /// </summary>
    [JsonPropertyName("provider")]
    public int Provider { get; set; }

    /// <summary>
    /// Agency
    /// </summary>
    [JsonPropertyName("agency")]
    public string? Agency { get; set; }

    /// <summary>
    /// Address
    /// </summary>
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    /// <summary>
    /// Tag
    /// </summary>
    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    /// <summary>
    /// State
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    /// Issue date
    /// </summary>
    [JsonPropertyName("issueDate")]
    public DateTime? IssueDate { get; set; }

    /// <summary>
    /// Start date
    /// </summary>
    [JsonPropertyName("startDate")]
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date
    /// </summary>
    [JsonPropertyName("endDate")]
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Amount
    /// </summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency
    /// </summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    /// <summary>
    /// Payment status
    /// </summary>
    [JsonPropertyName("paymentStatus")]
    public int PaymentStatus { get; set; }

    /// <summary>
    /// Fine type
    /// </summary>
    [JsonPropertyName("fineType")]
    public int FineType { get; set; }

    /// <summary>
    /// Note
    /// </summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    /// <summary>
    /// Link
    /// </summary>
    [JsonPropertyName("link")]
    public string? Link { get; set; }

    /// <summary>
    /// Is active flag
    /// </summary>
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
}