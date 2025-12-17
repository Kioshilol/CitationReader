using System.Text.Json.Serialization;

namespace CitationReader.Models.Citation;

public class VanguardResponse
{
    [JsonPropertyName("recordsFound")]
    public int RecordsFound { get; set; }

    [JsonPropertyName("notices")]
    public List<Notice> Notices { get; set; }
    
    public class Notice
    {
        [JsonPropertyName("sessionId")]
        public string? SessionId { get; set; }

        [JsonPropertyName("notice")]
        public string NoticeNumber { get; set; }

        [JsonPropertyName("noticeDate")]
        public NoticeDate NoticeDate { get; set; }

        [JsonPropertyName("entryTime")]
        public string EntryTime { get; set; }

        [JsonPropertyName("exitTime")]
        public string ExitTime { get; set; }

        [JsonPropertyName("ticketStatus")]
        public string TicketStatus { get; set; }

        [JsonPropertyName("ticketTimeStatus")]
        public string TicketTimeStatus { get; set; }

        [JsonPropertyName("isHandheld")]
        public bool IsHandheld { get; set; }

        [JsonPropertyName("inCollections")]
        public bool InCollections { get; set; }

        [JsonPropertyName("lpn")]
        public string Lpn { get; set; }

        [JsonPropertyName("lpnId")]
        public string LpnId { get; set; }

        [JsonPropertyName("lpnState")] 
        public string LpnState { get; set; }

        [JsonPropertyName("lotId")]
        public string LotId { get; set; }

        [JsonPropertyName("lotAddress")]
        public string LotAddress { get; set; }

        [JsonPropertyName("amountDue")]
        public string AmountDue { get; set; }

        [JsonPropertyName("ticketPaidOn")]
        public string? TicketPaidOn { get; set; }

        [JsonPropertyName("surchargeName")]
        public string SurchargeName { get; set; } = string.Empty;
    }
    
    public class NoticeDate
    {
        [JsonPropertyName("ts")]
        public long Ts { get; set; }

        [JsonPropertyName("invalid")]
        public string? Invalid { get; set; }

        [JsonPropertyName("weekData")]
        public string? WeekData { get; set; }

        [JsonPropertyName("localWeekData")]
        public string? LocalWeekData { get; set; }

        [JsonPropertyName("o")]
        public int O { get; set; }

        [JsonPropertyName("isLuxonDateTime")]
        public bool IsLuxonDateTime { get; set; }
    }
}