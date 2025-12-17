using CitationReader.Enums;

namespace CitationReader.Models.Citation.Internal;

public class CitationError
{
    public string Message { get; set; }

    public string CarDetails { get; set; }

    public int ErrorCode { get; set; }

    public DateTime Timestamp { get; set; }
    
    public CitationProviderType CitationProviderType { get; set; }

    public Dictionary<string, object>? AdditionalDetails { get; set; }

    public void AddDetail(string key, object value)
    {
        AdditionalDetails ??= new Dictionary<string, object>();
        AdditionalDetails[key] = value;
    }
}