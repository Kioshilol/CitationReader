using System.Globalization;
using CitationReader.Common;
using CitationReader.Enums;
using CitationReader.Extensions;
using CitationReader.Models.Base;
using CitationReader.Models.Citation;
using CitationReader.Models.Citation.Internal;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;

namespace CitationReader.Readers;

public class MiamiParkingReader : BaseHttpReader, ICitationReader
{
    private const string AuthKey = "05A8A773-CD13-41DD-81AF-FEC02DC9A14D";
    private const string Url = "https://www2.miamidadeclerk.gov/Developers/api/ParkingWeb";
    
    public MiamiParkingReader() 
        : base(HttpClientType.HttpCitationReader)
    {
        
    }

    public CitationProviderType SupportedProviderType => CitationProviderType.MiamiParking;
    public string Link => "https://www2.miamidadeclerk.gov/payparking/parkingSearch.aspx";
    
    public async Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsAsync(
        string licensePlate, 
        string state)
    {
       var carDetails = $"{licensePlate} ({state})";

       try
       {
           var requestUrl = $"{Url}?tag={licensePlate}&state={state}&AuthKey={AuthKey}";
           var response = await RequestAsync<MiamiParkingResponse>(
               HttpMethod.Get,
               requestUrl,
               null,
               CreateSuccessResponseCallback);
           if (!response.IsSuccess)
           {
               var errorMessage = response.Message ?? "API request failed";
               Logger.LogWarning(
                   "Miami Parking API request failed for {CarDetails}: {ErrorMessage}",
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

           var notices = response.Result?.ParkingCitation?.CitationModel;
           if (notices is null || !notices.Any())
           {
               Logger.LogInformation("No citations found for vehicle: {CarDetails}", carDetails);
               return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                   ArraySegment<CitationModel>.Empty,
                   state);
           }

           var citations = ProduceItems(
               notices,
               licensePlate,
               state).ToList();
           Logger.LogInformation("Found {Count} citations for vehicle: {CarDetails}", citations.Count, carDetails);

           return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(citations, state);

           bool CreateSuccessResponseCallback(BaseResponse<MiamiParkingResponse> baseResponse)
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
        IEnumerable<MiamiParkingResponse.MiamiCitationModel> items,
        string tag, 
        string state)
    {
        return items.Select(item => new CitationModel
        {
            CitationNumber = item.CitNumber,
            Agency = CitationProviderType.MiamiParking.GetDisplayName(),
            Address = item.CitLocation,
            Tag = tag,
            State = state,
            IssueDate = CombineDateTime(item.CitIssueDate, item.CitIssueTime),
            Amount = ParseAmountString(item.CitAmtDueNow),
            Currency = Constants.Currency,
            PaymentStatus = (int)PaymentStatus.New,
            FineType = (int)FineType.Parking,
            IsActive = true,
            Link = Link,
            Note = item.Comment,
        });
    }
    
    private static decimal ParseAmountString(string amountString)
    {
        if (string.IsNullOrWhiteSpace(amountString))
        {
            return 0m;
        }

        var cleanAmount = amountString.Trim()
            .Replace("$", "")
            .Replace(",", "");

        return decimal.TryParse(cleanAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result 
            : 0m;
    }
    
    private static DateTime? CombineDateTime(string date, string time)
    {
        if (string.IsNullOrWhiteSpace(date) && string.IsNullOrWhiteSpace(time))
        {
            return null;
        }
        
        var candidate = string.IsNullOrWhiteSpace(time) 
            ? date 
            :  $"{date} {time}";
        if (DateTime.TryParse(candidate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        if (DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d))
        {
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        }
        
        return null;
    }
}
