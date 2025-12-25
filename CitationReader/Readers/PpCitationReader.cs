using CitationReader.Common;
using CitationReader.Enums;
using CitationReader.Extensions;
using CitationReader.Models.Base;
using CitationReader.Models.Citation;
using CitationReader.Models.Citation.Internal;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;

namespace CitationReader.Readers;

public class PpCitationReader : BaseHttpReader, ICitationReader
{
    private const string Url = "https://api.cpmdashboard.com/v1/violationapp/plate/";
    
    public PpCitationReader() 
        : base(HttpClientType.HttpCitationReader)
    {
    }

    public CitationProviderType SupportedProviderType => CitationProviderType.ParkingCompliance;

    public string Link => "https://ppnotice.com/";
    
    public async Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsAsync(string licensePlate, string state)
    {
        var carDetails = $"{licensePlate} ({state})";

        try
        {
            var requestUrl = $"{Url}{Uri.EscapeDataString(licensePlate)}";
            var response = await RequestAsync<List<ParkingComplianceApiResponse>>(
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

            var notices = response.Result;
            if (notices is null || !notices.Any())
            {
                Logger.LogInformation("No citations found for vehicle: {CarDetails}", carDetails);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                    ArraySegment<CitationModel>.Empty,
                    state);
            }
            
            var citations = ProduceItems(notices, state).ToList();
            Logger.LogInformation("Found {Count} citations for vehicle: {CarDetails}", citations.Count, carDetails);
            
            return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(citations, state);

            bool CreateSuccessResponseCallback(BaseResponse<List<ParkingComplianceApiResponse>> baseResponse)
            {
                return false;
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
    
    private IEnumerable<CitationModel> ProduceItems(
        IEnumerable<ParkingComplianceApiResponse> items, 
        string state)
    {
        return items.Select(item => new CitationModel
        {
            NoticeNumber = item.NoticeNumber,
            Agency = SupportedProviderType.GetDisplayName(),
            Address = item.Lot.Address,
            StartDate = item.EntryTime,
            EndDate = item.ExitTime,
            Tag = item.PlateNumber,
            State = state,
            IssueDate = item.EntryTime,
            Amount = item.Fine,
            Currency = Constants.Currency,
            PaymentStatus = (int)(item.Status == "PAID" ? PaymentStatus.Paid : PaymentStatus.New),
            FineType = (int)FineType.Parking,
            IsActive = item.Status != "RESOLVED",
            Link = Link,
            Note = item.Status
        });
    }
}