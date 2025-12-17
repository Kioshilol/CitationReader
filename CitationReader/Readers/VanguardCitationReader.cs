using CitationReader.Common;
using CitationReader.Enums;
using CitationReader.Models.Citation;
using CitationReader.Models.Huur;
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

    public CitationType SupportedType => CitationType.Vanguard;
    public string Link => "https://www.payparkingnotice.com/";

    public async Task<IEnumerable<CitationDto>> ReadCitationsAsync(string licensePlate, string state)
    {
        Logger.LogInformation(
            "Reading citations from Vanguard for vehicle: {Tag} ({LicensePlate})",
            licensePlate, 
            state);

        var requestUrl = $"{Url}lookup?method=lpnLookup&lpn={Uri.EscapeDataString(licensePlate)}&lpnState={Uri.EscapeDataString(state)}&includeAll=true/";
        var response = await RequestAsync<VanguardResponse>(
            HttpMethod.Get, 
            requestUrl);
        if (!response.IsSuccess)
        {
            return null;
        }

        var notices = response.Result?.Notices;
        if (notices is null || notices.Count <= 0)
        {
            return ArraySegment<CitationDto>.Empty;
        }
        
        return ProduceItems(notices);
    }
    
    private IEnumerable<CitationDto> ProduceItems(IEnumerable<VanguardResponse.Notice> items)
    {
        foreach (var item in items)
        {
            var parkingViolation = new CitationDto
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
                Link = Link
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
