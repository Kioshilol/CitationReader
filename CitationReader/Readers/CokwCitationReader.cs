using CitationReader.Enums;
using CitationReader.Models.Citation.Internal;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;

namespace CitationReader.Readers;

public class CokwCitationReader : BaseRmcReader, ICitationReader
{
    public override CitationProviderType SupportedProviderType => CitationProviderType.CityOfKeyWest;
    public override string Link => "https://cityofkeywest.rmcpay.com";
    protected override string OperatorId => "1172";
    protected override string Url => "https://cityofkeywest.rmcpay.com";
    
    public Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsAsync(
        string licensePlate,
        string state)
    {
        return ReadRmcCitationsAsync(licensePlate, state);
    }
}
