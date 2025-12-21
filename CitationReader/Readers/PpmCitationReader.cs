using CitationReader.Enums;
using CitationReader.Models.Citation.Internal;
using CitationReader.Readers.Base;
using CitationReader.Readers.Interfaces;
using System.Text.RegularExpressions;
using CitationReader.Extensions;

namespace CitationReader.Readers;

public class PpmCitationReader : BaseParseReader, ICitationReader
{
    public PpmCitationReader() 
        : base(HttpClientType.ParseCitationReader)
    {
    }

    public override CitationProviderType SupportedProviderType => CitationProviderType.ProfessionalParkingManagement;
    public override string Link => "https://paymyviolations.com";
    protected override string BaseUrl => "https://paymyviolations.com/";
    protected override string ProviderName => CitationProviderType.ProfessionalParkingManagement.GetDisplayName();
    protected override string GetLicensePlateFieldName() => "plate_number";
    protected override string GetStateFieldName() => "plate_state";

    protected override string[] GetNoResultsIndicators() => new[]
    {
        "no citations",
        "no violations", 
        "not found"
    };

    protected override string[] GetCitationNumberPatterns() => new[]
    {
        @"citation[^:]*:\s*([A-Z0-9\-]+)",
        @"notice[^:]*:\s*([A-Z0-9\-]+)",
        @"violation[^:]*:\s*([A-Z0-9\-]+)"
    };

    protected override List<CitationModel> ParseProviderSpecificFormat(
        string html, 
        string licensePlate,
        string state)
    {
        return ParseCardBasedCitations(
            html, 
            licensePlate,
            state);
    }

    protected override string GetTokenFieldName() => "_token";
    
    private List<CitationModel> ParseCardBasedCitations(
        string html,
        string licensePlate,
        string state)
    {
        var citations = new List<CitationModel>();
        
        try
        {
            Logger.LogDebug("Attempting to parse card-based citation format for PPM");
            
            const string citationBlockPattern = @"<div[^>]*>[\s\S]*?(?:NOTICE\s+NUMBER|Notice\s+Number)[\s\S]*?</div>";
            var citationBlocks = Regex.Matches(
                html,
                citationBlockPattern, 
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (citationBlocks.Count == 0)
            {
                const string alternativePattern = @"(?:<tr[^>]*>|<div[^>]*>)[\s\S]*?(\d{3}-\d{3}-\d{3})[\s\S]*?(?:</tr>|</div>)";
                citationBlocks = Regex.Matches(
                    html, 
                    alternativePattern, 
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
            
            if (citationBlocks.Count == 0)
            {
                const string broadPattern = @"(?:<tr[^>]*>|<div[^>]*>|<td[^>]*>)[\s\S]*?(\d{3}[-\s]\d{3}[-\s]\d{3})[\s\S]*?(?:</tr>|</div>|</td>)";
                citationBlocks = Regex.Matches(
                    html,
                    broadPattern, 
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
            }
            
            Logger.LogDebug("Found {Count} potential citation blocks", citationBlocks.Count);
            
            foreach (Match block in citationBlocks)
            {
                var blockHtml = block.Value;
                var citation = ExtractCitationFromCardBlock(blockHtml, licensePlate, state);
                if (citation == null)
                {
                    continue;
                }
                
                citations.Add(citation);
                Logger.LogDebug("Successfully extracted citation: {NoticeNumber}", citation.NoticeNumber);
            }
            
            if (!citations.Any())
            {
                citations.AddRange(ExtractCitationsFromFullHtml(html, licensePlate, state));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing card-based citations for PPM");
        }
        
        return citations;
    }

    private CitationModel? ExtractCitationFromCardBlock(
        string blockHtml,
        string licensePlate,
        string state)
    {
        try
        {
            var cleanText = CleanHtmlText(blockHtml);
            
            var noticeNumberPatterns = new[]
            {
                @"(\d{3}-\d{3}-\d{3})",           // 274-380-862
                @"(\d{3}\s+\d{3}\s+\d{3})",       // 274 380 862
                @"(\d{9})",                       // 274380862
                @"Notice\s+Number[:\s]*(\d+[-\s]*\d+[-\s]*\d+)", // Notice Number: 274-380-862
                @"NOTICE\s+NUMBER[:\s]*(\d+[-\s]*\d+[-\s]*\d+)"  // NOTICE NUMBER: 274-380-862
            };
            
            string? noticeNumber = null;
            foreach (var pattern in noticeNumberPatterns)
            {
                var match = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    continue;
                }
                
                noticeNumber = match.Groups[1].Value.Replace(" ", "-");
                break;
            }
            
            if (string.IsNullOrEmpty(noticeNumber))
            {
                Logger.LogDebug("No notice number found in block");
                return null;
            }
            
            var amountPatterns = new[]
            {
                @"\$(\d+\.?\d*)",                 // $94.99
                @"Total[:\s]*\$?(\d+\.?\d*)",     // Total: $94.99
                @"Amount[:\s]*\$?(\d+\.?\d*)",    // Amount: $94.99
                @"(\d+\.?\d*)\s*USD"              // 94.99 USD
            };
            
            decimal amount = 0;
            foreach (var pattern in amountPatterns)
            {
                var matches = Regex.Matches(cleanText, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (!decimal.TryParse(match.Groups[1].Value, out var parsedAmount))
                    {
                        continue;
                    }
                    
                    if (parsedAmount > amount)
                    {
                        amount = parsedAmount;
                    }
                }
            }
            
            var datePatterns = new[]
            {
                @"(\d{1,2}/\d{1,2}/\d{4})",       // 08/22/2025
                @"(\d{4}-\d{1,2}-\d{1,2})",       // 2025-08-22
                @"(\w+\s+\d{1,2},?\s+\d{4})"      // August 22, 2025
            };
            
            var issueDate = DateTime.MinValue;
            foreach (var pattern in datePatterns)
            {
                var match = Regex.Match(cleanText, pattern, RegexOptions.IgnoreCase);
                if (!match.Success || !DateTime.TryParse(match.Groups[1].Value, out var parsedDate))
                {
                    continue;
                }
                
                issueDate = parsedDate;
                break;
            }
            
            const string locationPattern = @"(\d+\s+[A-Z\s]+(?:AVE|ST|RD|BLVD|DR|LN|CT|PL|WAY)[^,]*(?:,\s*[A-Z]{2}\s+\d{5})?)";
            var locationMatch = Regex.Match(cleanText, locationPattern, RegexOptions.IgnoreCase);
            var location = locationMatch.Success ? locationMatch.Groups[1].Value.Trim() : "";
            
            var paymentStatus = PaymentStatus.New;
            if (cleanText.Contains("paid", StringComparison.OrdinalIgnoreCase) ||
                cleanText.Contains("settled", StringComparison.OrdinalIgnoreCase))
            {
                paymentStatus = PaymentStatus.Paid;
            }
            
            var violationPatterns = new[]
            { 
                "Failure to Register or Pay in Advance",
                "Non Payment",
                "Expired Meter",
                "No Permit",
                "Overtime Parking"
            };
            
            var violationType = "";
            foreach (var pattern in violationPatterns)
            {
                if (!cleanText.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                violationType = pattern;
                break;
            }
            
            var citation = new CitationModel
            {
                NoticeNumber = noticeNumber,
                IssueDate = issueDate,
                Amount = amount,
                Agency = ProviderName,
                Tag = licensePlate,
                State = state,
                Currency = "USD",
                PaymentStatus = (int)paymentStatus,
                FineType = (int)FineType.Parking,
                IsActive = paymentStatus != PaymentStatus.Paid,
                Link = Link,
                CitationProviderType = SupportedProviderType,
                Address = location,
                Note = violationType
            };
            
            Logger.LogDebug(
                "Extracted citation from card block: {NoticeNumber}, Amount: {Amount}, Date: {Date}", 
                citation.NoticeNumber,
                citation.Amount, 
                citation.IssueDate);
            
            return citation;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error extracting citation from card block");
            return null;
        }
    }

    private List<CitationModel> ExtractCitationsFromFullHtml(
        string html,
        string licensePlate,
        string state)
    {
        var citations = new List<CitationModel>();
        
        try
        {
            Logger.LogDebug("Attempting to extract citations from full HTML for PPM");
            
            const string noticeNumberPattern = @"(\d{3}[-\s]\d{3}[-\s]\d{3})";
            var noticeMatches = Regex.Matches(html, noticeNumberPattern);
            
            var foundNoticeNumbers = new HashSet<string>();
            
            foreach (Match match in noticeMatches)
            {
                var noticeNumber = match.Groups[1].Value.Replace(" ", "-");

                if (!foundNoticeNumbers.Add(noticeNumber))
                {
                    continue;
                }

                var contextStart = Math.Max(0, match.Index - 500);
                var contextEnd = Math.Min(html.Length, match.Index + 500);
                var context = html.Substring(contextStart, contextEnd - contextStart);
                
                var citation = ExtractCitationFromCardBlock(context, licensePlate, state);
                if (citation != null)
                {
                    citations.Add(citation);
                }
                else
                {
                    citations.Add(new CitationModel
                    {
                        NoticeNumber = noticeNumber,
                        CitationNumber = noticeNumber,
                        Agency = ProviderName,
                        Tag = licensePlate,
                        State = state,
                        Currency = "USD",
                        PaymentStatus = (int)PaymentStatus.New,
                        FineType = (int)FineType.Parking,
                        IsActive = true,
                        Link = Link,
                        CitationProviderType = SupportedProviderType
                    });
                }
            }
            
            Logger.LogDebug("Extracted {Count} citations from full HTML", citations.Count);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error extracting citations from full HTML for PPM");
        }
        
        return citations;
    }

    private static string CleanHtmlText(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }
        
        var text = Regex.Replace(html, @"<[^>]+>", "");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }
}
