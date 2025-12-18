using CitationReader.Enums;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;

namespace CitationReader.Readers;

public class PpmCitationReader : BaseParseReader, ICitationReader
{
    public PpmCitationReader() 
        : base(HttpClientType.ParseCitationReader)
    {
    }

    public override CitationProviderType SupportedProviderType => CitationProviderType.ProfessionalParkingManagement;
    public override string Link => "https://paymyviolations.com";
    protected override string BaseUrl => "https://paymyviolations.com/";
    protected override string ProviderName => "Professional Parking Management";
    protected override string GetLicensePlateFieldName() => "plate_number";
    protected override string GetStateFieldName() => "plate_state";

    protected override string[] GetNoResultsIndicators() => new[]
    {
        "no citations",
        "no violations", 
        "not found"
    };

    protected override string[] GetCitationNumberPatterns() => new[]
    {
        @"citation[^:]*:\s*([A-Z0-9\-]+)",
        @"notice[^:]*:\s*([A-Z0-9\-]+)",
        @"violation[^:]*:\s*([A-Z0-9\-]+)"
    };
}
