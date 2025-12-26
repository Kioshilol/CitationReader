using CitationReader.Common;
using CitationReader.Enums;
using CitationReader.Extensions;
using CitationReader.Models.Base;
using CitationReader.Models.Citation;
using CitationReader.Models.Citation.Internal;

namespace CitationReader.Readers.Base;

public abstract class BaseRmcReader : BaseHttpReader
{
    public BaseRmcReader() 
        : base(HttpClientType.HttpCitationReader)
    {
    }

    public abstract CitationProviderType SupportedProviderType { get; }
    public abstract string Link { get; }
    protected abstract string OperatorId { get; }
    protected abstract string Url { get; }
    
    public async Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadRmcCitationsAsync(
        string licensePlate,
        string state)
    {
        var carDetails = $"{licensePlate} ({state})";

        try
        {
            var stateId = GetStateId(state);
            var requestUrl = $"{Url}/rmcapi/api/violation_index.php/searchviolation?operatorid={Uri.EscapeDataString(OperatorId)}&stateid={Uri.EscapeDataString(stateId)}&lpn={Uri.EscapeDataString(licensePlate)}";
            var response = await RequestAsync<RmcPayResponse>(
                HttpMethod.Get,
                requestUrl,
                null,
                CreateSuccessResponseCallback);
            if (!response.IsSuccess)
            {
                var errorMessage = response.Message ?? "API request failed";
                Logger.LogWarning(
                    "{Provider} request failed for {CarDetails}: {ErrorMessage}",
                    SupportedProviderType.GetDisplayName(),
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

            var violations = response.Result?.Data;
            if (violations is null || !violations.Any())
            {
                Logger.LogInformation("No citations found for vehicle: {CarDetails}", carDetails);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                    ArraySegment<CitationModel>.Empty,
                    state);
            }

            var citations = ProduceItems(violations).ToList();
            Logger.LogInformation("Found {Count} citations for vehicle: {CarDetails}", citations.Count, carDetails);

            return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(citations, state);
            
            bool CreateSuccessResponseCallback(BaseResponse<RmcPayResponse> baseResponse)
            {
                var errorMessage = baseResponse.Message ?? "";
                return errorMessage.Contains("No violation found for plate") ||
                       errorMessage.Contains("No violation found") || 
                       errorMessage.Contains("Violation closed for violation");
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

    private string GetStateId(string state)
    {
        var stateMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // US States
            { "AL", "81" }, { "Alabama", "81" },
            { "AK", "82" }, { "Alaska", "82" },
            { "AZ", "83" }, { "Arizona", "83" },
            { "AR", "84" }, { "Arkansas", "84" },
            { "CA", "85" }, { "California", "85" },
            { "CO", "86" }, { "Colorado", "86" },
            { "CT", "87" }, { "Connecticut", "87" },
            { "DE", "88" }, { "Delaware", "88" },
            { "DC", "131" }, { "District of Columbia", "131" },
            { "FL", "89" }, { "Florida", "89" },
            { "GA", "90" }, { "Georgia", "90" },
            { "GU", "663" }, { "Guam", "663" },
            { "HI", "91" }, { "Hawaii", "91" },
            { "ID", "92" }, { "Idaho", "92" },
            { "IL", "93" }, { "Illinois", "93" },
            { "IN", "94" }, { "Indiana", "94" },
            { "IA", "95" }, { "Iowa", "95" },
            { "KS", "96" }, { "Kansas", "96" },
            { "KY", "97" }, { "Kentucky", "97" },
            { "LA", "98" }, { "Louisiana", "98" },
            { "ME", "99" }, { "Maine", "99" },
            { "MD", "100" }, { "Maryland", "100" },
            { "MA", "101" }, { "Massachusetts", "101" },
            { "MI", "102" }, { "Michigan", "102" },
            { "MN", "103" }, { "Minnesota", "103" },
            { "MS", "104" }, { "Mississippi", "104" },
            { "MO", "105" }, { "Missouri", "105" },
            { "MT", "106" }, { "Montana", "106" },
            { "NE", "107" }, { "Nebraska", "107" },
            { "NV", "108" }, { "Nevada", "108" },
            { "NH", "109" }, { "New Hampshire", "109" },
            { "NJ", "110" }, { "New Jersey", "110" },
            { "NM", "111" }, { "New Mexico", "111" },
            { "NY", "112" }, { "New York", "112" },
            { "NC", "113" }, { "North Carolina", "113" },
            { "ND", "114" }, { "North Dakota", "114" },
            { "OH", "115" }, { "Ohio", "115" },
            { "OK", "116" }, { "Oklahoma", "116" },
            { "OR", "117" }, { "Oregon", "117" },
            { "PA", "118" }, { "Pennsylvania", "118" },
            { "PR", "495" }, { "Puerto Rico", "495" },
            { "RI", "119" }, { "Rhode Island", "119" },
            { "SC", "120" }, { "South Carolina", "120" },
            { "SD", "121" }, { "South Dakota", "121" },
            { "TN", "122" }, { "Tennessee", "122" },
            { "TX", "123" }, { "Texas", "123" },
            { "UT", "124" }, { "Utah", "124" },
            { "VT", "125" }, { "Vermont", "125" },
            { "VI", "613" }, { "Virgin Islands", "613" },
            { "VA", "126" }, { "Virginia", "126" },
            { "WA", "127" }, { "Washington", "127" },
            { "WV", "128" }, { "West Virginia", "128" },
            { "WI", "129" }, { "Wisconsin", "129" },
            { "WY", "130" }, { "Wyoming", "130" },

            // Canadian Provinces
            { "AB", "193" }, { "Alberta", "193" },
            { "BC", "194" }, { "British Columbia", "194" },
            { "MB", "195" }, { "Manitoba", "195" },
            { "NB", "196" }, { "New Brunswick", "196" },
            { "NL", "197" }, { "Newfoundland and Labrador", "197" },
            { "NT", "198" }, { "Northwest Territories", "198" },
            { "NS", "199" }, { "Nova Scotia", "199" },
            { "NU", "200" }, { "Nunavut", "200" },
            { "ON", "201" }, { "Ontario", "201" },
            { "PE", "202" }, { "Prince Edward Island", "202" },
            { "QC", "203" }, { "Quebec", "203" },
            { "SK", "204" }, { "Saskatchewan", "204" },
            { "YT", "205" }, { "Yukon", "205" }
        };

        return stateMapping.GetValueOrDefault(state, state);
    }

    private IEnumerable<CitationModel> ProduceItems(IEnumerable<RmcPayResponse.ViolationData> items)
    {
        return items.Select(item => new CitationModel
        {
            CitationNumber = item.ViolationNumber,
            NoticeNumber = item.Number,
            Agency = item.OperatorDisplayName,
            Address = item.Location ?? item.Zone,
            Tag = item.Lpn,
            State = item.VehicleState,
            IssueDate = item.Date.ParseToDateTime(),
            StartDate = item.Date.ParseToDateTime(),
            EndDate = item.SettlementDate.ParseToDateTime(),
            Amount = decimal.TryParse(item.AmountInCents, out var amountCents) ? amountCents / 100 : 0,
            Currency = Constants.Currency,
            PaymentStatus = (int)PaymentHelper.GetStatus(item.Status),
            FineType = (int)FineType.Parking,
            IsActive = item.Status.ToLower() != "paid" && item.Paid != "1",
            Link = Link,
            Note = item.Notes?.Any() == true 
                ? string.Join("; ", item.Notes.Select(n => n.NoteText)) 
                : string.Empty
        });
    }
    
}
