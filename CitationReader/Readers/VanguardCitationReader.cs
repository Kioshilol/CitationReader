using System.Text.Json;
using CitationReader.Common;
using CitationReader.Enums;
using CitationReader.Models.Citation;
using CitationReader.Models.Huur;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;

namespace CitationReader.Readers;

public class VanguardCitationReader : HttpReader, ICitationReader
{
    private const string Name = "Vanguard";
    private const string Url = "https://www.payparkingnotice.com/api/";
    
    private readonly ILogger<VanguardCitationReader> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    

    public VanguardCitationReader(
        ILogger<VanguardCitationReader> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    public CitationType SupportedType => CitationType.Vanguard;

    public async Task<IEnumerable<CitationDto>> ReadCitationsAsync(string licensePlate, string state)
    {
        _logger.LogInformation(
            "Reading citations from Vanguard for vehicle: {Tag} ({LicensePlate})",
            licensePlate, 
            state);

        var requestUrl =
            $"{Url}lookup?method=lpnLookup&lpn={Uri.EscapeDataString(licensePlate)}&lpnState={Uri.EscapeDataString(state)}&includeAll=true/";

        var response = await HttpClient.GetAsync(requestUrl);
        if (!response.IsSuccessStatusCode)
        {
            return ArraySegment<CitationDto>.Empty;
        }
        
        var jsonContent = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<VanguardResponse.VanGuardResponse>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (apiResponse is { RecordsFound: > 0 })
        {
            return ProduceItems();
        }
        
        return ArraySegment<CitationDto>.Empty;

        IEnumerable<CitationDto> ProduceItems()
        {
            foreach (var violation in apiResponse.Notices)
            {
                var parkingViolation = new CitationDto
                {
                    NoticeNumber = violation.NoticeNumber,
                    Agency = Name,
                    Address = violation.LotAddress,
                    Tag = violation.Lpn,
                    State = violation.LpnState,
                    IssueDate = violation.NoticeDate == null
                        ? null
                        : DateTimeOffset.FromUnixTimeMilliseconds(violation.NoticeDate.Ts).DateTime,
                    StartDate = ParseDateTime(violation.EntryTime),
                    EndDate = ParseDateTime(violation.ExitTime),
                    Amount = decimal.TryParse(violation.AmountDue, out var amount) ? amount : 0,
                    Currency = "USD",
                    PaymentStatus = violation.TicketStatus.ToLower() != "paid" ? Constants.FineConstants.PNew : Constants.FineConstants.PPaid,
                    FineType = Constants.FineConstants.FtParking,
                    IsActive = violation.TicketStatus.ToLower() != "paid",
                    Link = Url
                };

                yield return parkingViolation;
            }
        }
    }
    
    private static DateTime ParseDateTime(string dateTimeString)
    {
        return DateTime.TryParse(dateTimeString, out var result) 
            ? result 
            : DateTime.MinValue;
    }
}
