using CitationReader.Common;
using CitationReader.Enums;
using CitationReader.Extensions;
using CitationReader.Models.Base;
using CitationReader.Models.Citation;
using CitationReader.Models.Citation.Internal;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;

namespace CitationReader.Readers;

public class VanguardCitationReader : BaseHttpReader, ICitationReader
{
    private const string Url = "https://www.payparkingnotice.com/api/";
    
    public VanguardCitationReader()
        : base(HttpClientType.HttpCitationReader)
    {
    }

    public CitationProviderType SupportedProviderType => CitationProviderType.Vanguard;
    public string Link => "https://www.payparkingnotice.com/";

    public async Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsAsync(
        string licensePlate,
        string state)
    {
        var carDetails = $"{licensePlate} ({state})";
        
        try
        {
            var requestUrl = $"{Url}lookup?method=lpnLookup&lpn={Uri.EscapeDataString(licensePlate)}&lpnState={Uri.EscapeDataString(state)}&includeAll=true/";
            var response = await RequestAsync<VanguardResponse>(
                HttpMethod.Get,
                requestUrl,
                null,
                CreateSuccessResponseCallback);
            if (!response.IsSuccess)
            {
                var errorMessage = response.Message ?? "API request failed";
                Logger.LogWarning(
                    "Vanguard API request failed for {CarDetails}: {ErrorMessage}",
                    carDetails,
                    errorMessage);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                    ArraySegment<CitationModel>.Empty,
                    state);
                //TODO: uncomment it if there is api
                // return BaseCitationResult<IEnumerable<CitationModel>>.CreateError(
                //     errorMessage,
                //     SupportedProviderType,
                //     carDetails,
                //     state,
                //     response.Reason);
            }

            var notices = response.Result?.Notices;
            if (notices is null || !notices.Any())
            {
                Logger.LogInformation("No citations found for vehicle: {CarDetails}", carDetails);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                    ArraySegment<CitationModel>.Empty,
                    state);
            }
            
            var citations = ProduceItems(notices).ToList();
            Logger.LogInformation("Found {Count} citations for vehicle: {CarDetails}", citations.Count, carDetails);
            
            return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(citations, state);

            bool CreateSuccessResponseCallback(BaseResponse<VanguardResponse> baseResponse)
            {
                var errorMessage = baseResponse.Message ?? "";
                return errorMessage.Contains("Error fetching data from external API") ||
                       errorMessage.Contains("API request failed: 404 Not Found");
            }
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
                Agency = CitationProviderType.Vanguard.GetDisplayName(),
                Address = item.LotAddress,
                Tag = item.Lpn,
                State = item.LpnState,
                IssueDate = item.NoticeDate == null
                    ? null
                    : DateTimeOffset.FromUnixTimeMilliseconds(item.NoticeDate.Ts).DateTime,
                StartDate = ParseDateTime(item.EntryTime),
                EndDate = ParseDateTime(item.ExitTime),
                Amount = decimal.TryParse(item.AmountDue, out var amount) ? amount : 0,
                Currency = Constants.Currency,
                PaymentStatus = item.TicketStatus.ToLower() != "Ready" 
                    ? (int)PaymentStatus.New
                    : (int)PaymentStatus.Paid,
                FineType = (int)FineType.Parking,
                IsActive = item.TicketStatus.ToLower() != "Ready",
                Link = Link,
                CitationProviderType = SupportedProviderType,
                Note = item.TicketStatus
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