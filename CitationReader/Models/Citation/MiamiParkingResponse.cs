using System.Text.Json.Serialization;

namespace CitationReader.Models.Citation;

public class MiamiParkingResponse
{
    [JsonPropertyName("Status")] 
    public string Status { get; set; }
    
    [JsonPropertyName("StatusDesc")] 
    public string StatusDesc { get; set; }
    
    [JsonPropertyName("ParkingCitation")] 
    public MiamiParkingCitationBlock? ParkingCitation { get; set; }
    
    public class MiamiParkingCitationBlock
    {
        [JsonPropertyName("citationModel")] 
        public List<MiamiCitationModel> CitationModel { get; set; }
        
        [JsonPropertyName("taginfoModel")] 
        public MiamiTagInfoModel TagInfo { get; set; }
    }
    
    public class MiamiCitationModel
    {
        [JsonPropertyName("cit_number")]
        public string CitNumber { get; set; }
        
        [JsonPropertyName("cit_issue_date")]
        public string CitIssueDate { get; set; }  
        
        [JsonPropertyName("cit_issue_time")] 
        public string CitIssueTime { get; set; }  
        
        [JsonPropertyName("cit_due_date")]
        public string CitDueDate { get; set; }
        
        [JsonPropertyName("cit_amt_due_now")]
        public string CitAmtDueNow { get; set; }
        
        [JsonPropertyName("cit_status_desc")]
        public string CitStatusDesc { get; set; }
        
        [JsonPropertyName("cit_cagency_name")]
        public string CitAgencyName { get; set; }
        
        [JsonPropertyName("cit_vio_code")]
        public string CitVioCode { get; set; }
        
        [JsonPropertyName("cit_vio_desc")]
        public string CitVioDesc { get; set; }
        
        [JsonPropertyName("cit_location")]
        public string CitLocation { get; set; }
        
        [JsonPropertyName("cit_public_comment")]
        public string Comment { get; set; }
    }
    
    public class MiamiTagInfoModel
    {
        [JsonPropertyName("tag_number")] 
        public string TagNumber { get; set; }
        
        [JsonPropertyName("tag_state")]
        public string TagState { get; set; }
    }
}