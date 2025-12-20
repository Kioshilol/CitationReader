using CitationReader.Enums;
using CitationReader.Models.Citation.Internal;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;

namespace CitationReader.Readers;

public class CoflCitationReader : BaseHostedPortalParseReader, ICitationReader
{
    private const string Name = "City of Fort Lauderdale";
    private const string Url = "https://fortlauderdaleparking.t2hosted.com";

    public CoflCitationReader() 
        : base(Url, Name)
    {
    }
    
    public CitationProviderType SupportedProviderType => CitationProviderType.CityOfFortLauderdale;
    
    public string Link => Url;

    public async Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsAsync(string licensePlate, string state)
    {
        try
        {
            var carDetails = $"{licensePlate} ({state})";
            
            var response = await SearchCitationAsync(licensePlate, state);
            if (!response.IsSuccess)
            {
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateError(
                    response.Message ?? "Search failed",
                    SupportedProviderType,
                    $"{licensePlate} ({state})",
                    state,
                    response.Reason);
            }

            if (response.Result is null || response.Result.Any())
            {
                Logger.LogInformation("No citations found for vehicle: {CarDetails}", carDetails);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                    ArraySegment<CitationModel>.Empty,
                    state);
            }

            var citations = response.Result?.Select(pv => new CitationModel
            {
                CitationNumber = pv.CitationNumber,
                NoticeNumber = pv.Note,
                Amount = pv.Amount,
                Currency = pv.Currency,
                IssueDate = pv.IssueDate,
                PaymentStatus = pv.PaymentStatus,
                FineType = pv.FineType,
                Agency = pv.Agency,
                Tag = pv.Tag,
                State = pv.State,
                Address = pv.Address,
                IsActive = pv.IsActive,
                Link = pv.Link,
                CitationProviderType = SupportedProviderType
            });

            return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(citations, state);
        }
        catch (Exception ex)
        {
            return BaseCitationResult<IEnumerable<CitationModel>>.CreateError(
                $"Exception occurred: {ex.Message}",
                SupportedProviderType,
                $"{licensePlate} ({state})",
                state,
                -1);
        }
    }
}
