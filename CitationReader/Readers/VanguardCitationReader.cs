using CitationReader.Common;
using CitationReader.Enums;
using CitationReader.Models.Citation;
using CitationReader.Models.Citation.Internal;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;

namespace CitationReader.Readers;

public class VanguardCitationReader : BaseHttpReader, ICitationReader
{
    private const string Name = "Vanguard";
    private const string Url = "https://www.payparkingnotice.com/api/";
    
    public VanguardCitationReader() 
        : base(HttpClientType.CitationReader)
    {
    }

    public CitationProviderType SupportedProviderType => CitationProviderType.Vanguard;
    public string Link => "https://www.payparkingnotice.com/";

    public async Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsWithResponseAsync(
        string licensePlate,
        string state)
    {
        var carDetails = $"{licensePlate} ({state})";
        
        try
        {
            var requestUrl = $"{Url}lookup?method=lpnLookup&lpn={Uri.EscapeDataString(licensePlate)}&lpnState={Uri.EscapeDataString(state)}&includeAll=true/";
            var response = await RequestAsync<VanguardResponse>(HttpMethod.Get, requestUrl);
            if (!response.IsSuccess)
            {
                var errorMessage = response.Message ?? "API request failed";
                Logger.LogWarning("Vanguard API request failed for {CarDetails}: {ErrorMessage}", carDetails, errorMessage);
                
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateError(
                    errorMessage,
                    SupportedProviderType,
                    carDetails,
                    state,
                    response.Reason);
            }

            var notices = response.Result?.Notices;
            if (notices is null || notices.Count <= 0)
            {
                Logger.LogInformation("No citations found for vehicle: {CarDetails}", carDetails);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                    ArraySegment<CitationModel>.Empty,
                    state);
            }
            
            var citations = ProduceItems(notices).ToList();
            Logger.LogInformation("Found {Count} citations for vehicle: {CarDetails}", citations.Count, carDetails);
            
            return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(citations, state);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Exception occurred while reading citations for vehicle: {CarDetails}",
                carDetails);
            
            return BaseCitationResult<IEnumerable<CitationModel>>.CreateError(
                "Exception occurred while reading citations: " + ex.Message,
                SupportedProviderType,
                carDetails,
                state,
                -1);
        }
    }
    
    private IEnumerable<CitationModel> ProduceItems(IEnumerable<VanguardResponse.Notice> items)
    {
        foreach (var item in items)
        {
            var parkingViolation = new CitationModel
            {
                NoticeNumber = item.NoticeNumber,
                Agency = Name,
                Address = item.LotAddress,
                Tag = item.Lpn,
                State = item.LpnState,
                IssueDate = item.NoticeDate == null
                    ? null
                    : DateTimeOffset.FromUnixTimeMilliseconds(item.NoticeDate.Ts).DateTime,
                StartDate = ParseDateTime(item.EntryTime),
                EndDate = ParseDateTime(item.ExitTime),
                Amount = decimal.TryParse(item.AmountDue, out var amount) ? amount : 0,
                Currency = "USD",
                PaymentStatus = item.TicketStatus.ToLower() != "paid" 
                    ? Constants.FineConstants.PNew 
                    : Constants.FineConstants.PPaid,
                FineType = Constants.FineConstants.FtParking,
                IsActive = item.TicketStatus.ToLower() != "paid",
                Link = Link,
                CitationProviderType = SupportedProviderType
            };

            yield return parkingViolation;
        }
    }
    
    private static DateTime ParseDateTime(string dateTimeString)
    {
        return DateTime.TryParse(dateTimeString, out var result) 
            ? result 
            : DateTime.MinValue;
    }
}
