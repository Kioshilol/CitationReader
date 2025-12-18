using CitationReader.Common;
using CitationReader.Enums;
using CitationReader.Models.Base;
using CitationReader.Models.Citation;
using CitationReader.Models.Citation.Internal;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;

namespace CitationReader.Readers;

public class MetropolisCitationReader : BaseHttpReader, ICitationReader
{
    private const string Name = "Metropolis";
    private const string Url = "https://site.metropolis.io/api/violation/customer/violations/";

    public MetropolisCitationReader()
        : base(HttpClientType.HttpCitationReader)
    {
    }

    public CitationProviderType SupportedProviderType => CitationProviderType.Metropolis;
    public string Link => "https://payments.metropolis.io/";

    public async Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsWithResponseAsync(
        string licensePlate,
        string state)
    {
        var carDetails = $"{licensePlate} ({state})";

        try
        {
            var requestUrl = $"{Url}search?licensePlateText={Uri.EscapeDataString(licensePlate)}&licensePlateState={Uri.EscapeDataString(state)}";
            var response = await RequestAsync<MetropolisApiResponse>(
                HttpMethod.Get,
                requestUrl,
                null,
                CreateSuccessResponseCallback);
            if (!response.IsSuccess)
            {
                var errorMessage = response.Message ?? "API request failed";
                Logger.LogWarning(
                    "Metropolis API request failed for {CarDetails}: {ErrorMessage}",
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

            var violations = response.Result?.Data.Violations;
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
            
            bool CreateSuccessResponseCallback(BaseResponse<MetropolisApiResponse> baseResponse)
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

    private IEnumerable<CitationModel> ProduceItems(IEnumerable<MetropolisApiResponse.Violation> items)
    {
        foreach (var item in items)
        {
            var parkingViolation = new CitationModel
            {
                NoticeNumber = item.ExtId,
                Agency = Name,
                Address = $"{item.ViolationItemView.SiteAddressInfo.Street}," +
                          $" {item.ViolationItemView.SiteAddressInfo.City}," +
                          $" {item.ViolationItemView.SiteAddressInfo.StateCode}" +
                          $" {item.ViolationItemView.SiteAddressInfo.Zip}",
                Tag = item.ViolationItemView.LicensePlate,
                State = item.ViolationItemView.LicensePlateState,
                IssueDate = DateTimeOffset.FromUnixTimeMilliseconds(item.ViolationItemView.ViolationIssued).DateTime,
                StartDate = DateTimeOffset.FromUnixTimeMilliseconds(item.ViolationItemView.VisitStart).DateTime,
                EndDate = DateTimeOffset.FromUnixTimeMilliseconds(item.ViolationItemView.VisitEnd).DateTime,
                Amount = item.ViolationItemView.TotalAmount,
                Currency = Constants.Currency,
                PaymentStatus = Constants.FineConstants.PNew,
                FineType = Constants.FineConstants.FtParking,
                IsActive = true,
                Link = Link
            };

            yield return parkingViolation;
        }
    }
}
