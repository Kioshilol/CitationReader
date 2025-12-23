using CitationReader.Enums;
using CitationReader.Models.Citation.Internal;
using CitationReader.Readers.Interfaces;
using System.Text.RegularExpressions;
using CitationReader.Extensions;
using CitationReader.Models.Base;

namespace CitationReader.Readers;

public class PpmCitationReader : ICitationReader
{
    private const string BaseUrl = "https://paymyviolations.com/";
    
    private readonly string _providerName = CitationProviderType.ProfessionalParkingManagement.GetDisplayName();
    private readonly HttpClient _httpClient;
    private readonly ILogger<PpmCitationReader> _logger;
    
    public PpmCitationReader(
        IHttpClientFactory httpClientFactory,
        ILogger<PpmCitationReader> logger)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientType.ParseCitationReader.ToString());
        _logger = logger;
    }

    public CitationProviderType SupportedProviderType => CitationProviderType.ProfessionalParkingManagement;
    public string Link => "https://paymyviolations.com";

    public async Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsAsync(
        string licensePlate,
        string state)
    {
        state = state.ToUpper();
        var carDetails = $"{licensePlate} ({state})";
        
        try
        {
            _logger.LogInformation("Starting citation search for {CarDetails} using PPM reader", carDetails);
            
            // Step 1: Get the initial form page
            var initialResponse = await GetPageAsync(BaseUrl);
            if (!initialResponse.IsSuccess)
            {
                _logger.LogWarning("Failed to load initial form page: {Error}", initialResponse.Message);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateError(
                    "Failed to load search form",
                    SupportedProviderType,
                    carDetails,
                    state,
                    initialResponse.Reason);
            }

            // Step 2: Create form data and extract tokens
            var pageContent = initialResponse.Result;
            var formData = new List<KeyValuePair<string, string>>
            {
                new("plate_number", licensePlate),
                new("plate_state", "FL_89")
            };
            
            // Extract CSRF token
            if (!string.IsNullOrEmpty(pageContent))
            {
                ExtractAndAddTokens(pageContent, formData);
            }
            
            var formActionUrl = ExtractFormActionUrl(pageContent);
            var formContent = new FormUrlEncodedContent(formData);
            
            // Add delay to mimic human behavior
            await Task.Delay(1500);
            
            // Step 3: Submit form
            var submitResponse = await SubmitFormAsync(formActionUrl, formContent);
            if (submitResponse.Message.Contains("Token"))
            {
                
            }
            if (!submitResponse.IsSuccess)
            {
                _logger.LogWarning("Form submission failed for {CarDetails}: {Error}", carDetails, submitResponse.Message);
                
                if (submitResponse.Message?.Contains("Token not provided", StringComparison.OrdinalIgnoreCase) == true ||
                    submitResponse.Message?.Contains("no citations", StringComparison.OrdinalIgnoreCase) == true ||
                    submitResponse.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                {
                    _logger.LogInformation("No citations found for {CarDetails}", carDetails);
                    return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                        ArraySegment<CitationModel>.Empty,
                        state);
                }
                
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateError(
                    submitResponse.Message ?? "Form submission failed",
                    SupportedProviderType,
                    carDetails,
                    state,
                    submitResponse.Reason);
            }

            // Step 4: Handle response (JSON redirect or direct HTML)
            var responseContent = submitResponse.Result;
            string? citationPageHtml;
            if (responseContent.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase) && 
                responseContent.Contains("\"location\":", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Form submission returned JSON redirect response");
                
                var redirectLocation = ExtractRedirectLocation(responseContent);
                if (!string.IsNullOrEmpty(redirectLocation))
                {
                    _logger.LogInformation("Following redirect to citation page: {Location}", redirectLocation);
                    
                    var citationPageResponse = await GetPageAsync(redirectLocation);
                    if (citationPageResponse.IsSuccess)
                    {
                        citationPageHtml = citationPageResponse.Result;
                        _logger.LogInformation("Successfully retrieved citation page HTML");
                    }
                    else
                    {
                        _logger.LogWarning("Failed to retrieve citation page: {Error}", citationPageResponse.Message);
                        return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                            ArraySegment<CitationModel>.Empty,
                            state);
                    }
                }
                else
                {
                    _logger.LogWarning("Could not extract redirect location from JSON response");
                    return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                        ArraySegment<CitationModel>.Empty,
                        state);
                }
            }
            else
            {
                citationPageHtml = responseContent;
                _logger.LogInformation("Form submission returned direct HTML response");
            }

            // Step 5: Parse citations from HTML
            var citations = ParseCitationsFromHtml(citationPageHtml, licensePlate, state);
            if (!citations.Any())
            {
                _logger.LogInformation("No citations found for vehicle: {CarDetails}", carDetails);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                    ArraySegment<CitationModel>.Empty,
                    state);
            }
            
            _logger.LogInformation("Found {Count} citations for vehicle: {CarDetails}", citations.Count, carDetails);
            
            return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(citations, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while reading citations for vehicle: {CarDetails}", carDetails);
            
            return BaseCitationResult<IEnumerable<CitationModel>>.CreateError(
                "Exception occurred while reading citations: " + ex.Message,
                SupportedProviderType,
                carDetails,
                state,
                -1);
        }
    }

    private List<CitationModel> ParseCitationsFromHtml(string? html, string licensePlate, string state)
    {
        var citations = new List<CitationModel>();
        if (string.IsNullOrEmpty(html))
        {
            return citations;
        }
        
        try
        {
            _logger.LogDebug("Starting HTML parsing for PPM citations");
            _logger.LogDebug("HTML length: {Length}", html.Length);
            
            // Check for no results indicators
            var noResultsIndicators = new[] { "no citations", "no violations", "not found" };
            if (noResultsIndicators.Any(indicator => html.Contains(indicator, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogDebug("Found no results indicator in HTML");
                return citations;
            }
            
            // First, try to parse as single notice format
            var singleCitation = ParseSingleNoticeFormat(html, licensePlate, state);
            if (singleCitation != null)
            {
                citations.Add(singleCitation);
                _logger.LogDebug("Successfully parsed single notice format");
                return citations;
            }
            
            // If not single notice, parse as multiple notices format
            _logger.LogDebug("Single notice format not detected, trying multiple notices format");
            
            // Find all citation numbers in the format XXX-XXX-XXX
            const string noticeNumberPattern = @"(\d{3}-\d{3}-\d{3})";
            var noticeMatches = Regex.Matches(html, noticeNumberPattern);
            
            _logger.LogDebug("Found {Count} potential notice numbers", noticeMatches.Count);
            
            var foundNoticeNumbers = new HashSet<string>();
            
            foreach (Match match in noticeMatches)
            {
                var noticeNumber = match.Groups[1].Value;
                
                if (!foundNoticeNumbers.Add(noticeNumber))
                {
                    continue; // Skip duplicates
                }
                
                _logger.LogDebug("Processing notice number: {NoticeNumber}", noticeNumber);
                
                // Extract context around this notice number - larger window to capture details panel
                var contextStart = Math.Max(0, match.Index - 2000);
                var contextEnd = Math.Min(html.Length, match.Index + 4000);
                var context = html.Substring(contextStart, contextEnd - contextStart);
                
                // Extract citation data from this context
                var citation = ExtractCitationFromContext(context, noticeNumber, licensePlate, state);
                if (citation != null)
                {
                    citations.Add(citation);
                    _logger.LogDebug("Successfully extracted citation: {NoticeNumber}, Amount: {Amount}, Address: {Address}", 
                        citation.NoticeNumber, citation.Amount, citation.Address);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing citations from HTML");
        }
        
        return citations;
    }
    
    private CitationModel? ParseSingleNoticeFormat(string html, string licensePlate, string state)
    {
        try
        {
            var hasLabelDivStructure = html.Contains(@"<div class=""label"">", StringComparison.OrdinalIgnoreCase);
            var hasNoticeNumber = html.Contains("NOTICE NUMBER", StringComparison.OrdinalIgnoreCase) || 
                                  html.Contains("Notice Number", StringComparison.OrdinalIgnoreCase);
            var hasLicensePlate = html.Contains("LICENSE PLATE", StringComparison.OrdinalIgnoreCase) || 
                                  html.Contains("License Plate", StringComparison.OrdinalIgnoreCase);
            
            // Count citation numbers to distinguish single vs multiple notices
            var citationNumberMatches = Regex.Matches(html, @"\d{3}-\d{3}-\d{3}");
            var citationCount = citationNumberMatches.Count;
            
            _logger.LogDebug("HTML contains div class label structure: {HasLabelDivStructure}", hasLabelDivStructure);
            _logger.LogDebug("HTML contains citation numbers count: {CitationCount}", citationCount);
            
            // Single notice format should have:
            // 1. The div class="label" structure (specific to single notice pages)
            // 2. Only ONE citation number
            // 3. Notice Number and License Plate fields
            if (!hasLabelDivStructure || citationCount != 1 || !hasNoticeNumber || !hasLicensePlate)
            {
                _logger.LogDebug("Single notice format not detected - LabelDiv: {HasLabelDiv}, CitationCount: {Count}, NoticeNumber: {HasNoticeNumber}, LicensePlate: {HasLicensePlate}", 
                    hasLabelDivStructure, citationCount, hasNoticeNumber, hasLicensePlate);
                return null;
            }
            
            _logger.LogDebug("Single notice format detected - has label div structure and exactly 1 citation number");
            
            // Extract notice number with more flexible patterns
            string? noticeNumber = null;
            var noticeNumberPatterns = new[]
            {
                @"NOTICE\s+NUMBER[^>]*>([^<]*(\d{3}-\d{3}-\d{3})[^<]*)",     // NOTICE NUMBER>216-291-764
                @"Notice\s+Number[^>]*>([^<]*(\d{3}-\d{3}-\d{3})[^<]*)",     // Notice Number>216-291-764
                @"NOTICE\s+NUMBER.*?(\d{3}-\d{3}-\d{3})",                    // NOTICE NUMBER anywhere before 216-291-764
                @"Notice\s+Number.*?(\d{3}-\d{3}-\d{3})",                    // Notice Number anywhere before 216-291-764
                @"(\d{3}-\d{3}-\d{3})"                                       // Just find any 216-291-764 pattern
            };
            
            foreach (var pattern in noticeNumberPatterns)
            {
                var noticeNumberMatch = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (noticeNumberMatch.Success)
                {
                    // For patterns with 2 groups, use the second group (the number), otherwise use the first
                    noticeNumber = noticeNumberMatch.Groups.Count > 2 ? noticeNumberMatch.Groups[2].Value : noticeNumberMatch.Groups[1].Value;
                    _logger.LogDebug("Found notice number: {NoticeNumber} using pattern: {Pattern}", noticeNumber, pattern);
                    break;
                }
            }
            
            if (string.IsNullOrEmpty(noticeNumber))
            {
                _logger.LogDebug("Could not extract notice number from single notice format");
                return null;
            }
            
            // Extract amount - look for "Amount Due" with more flexible patterns
            decimal amount = 0;
            var amountPatterns = new[]
            {
                @"Amount\s+Due[^>]*>\$(\d+\.\d{2})",           // Amount Due>$59.99
                @"Amount\s+Due[^$]*\$(\d+\.\d{2})",           // Amount Due ... $59.99
                @">Amount\s+Due<[^$]*\$(\d+\.\d{2})",         // >Amount Due< ... $59.99
                @"Amount\s+Due.*?\$(\d+\.\d{2})",             // Amount Due anywhere before $59.99
                @"Amount\s+Due\s*\n\s*\$(\d+\.\d{2})",        // Amount Due\n$59.99 (with line break)
                @"Amount\s+Due\s*<[^>]*>\s*\$(\d+\.\d{2})",   // Amount Due<br>$59.99 (with HTML break)
                @"\$(\d+\.\d{2})[^>]*Amount\s+Due",           // $59.99 before Amount Due
                @"<td[^>]*>\$(\d+\.\d{2})</td>"               // Table cell with amount
            };
            
            foreach (var pattern in amountPatterns)
            {
                var amountMatch = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (amountMatch.Success && decimal.TryParse(amountMatch.Groups[1].Value, out amount))
                {
                    _logger.LogDebug("Found amount: {Amount} using pattern: {Pattern}", amount, pattern);
                    break;
                }
            }
            
            // Extract Entry Date/Time with more flexible patterns
            DateTime? entryDate = null;
            var entryDatePatterns = new[]
            {
                @"ENTRY\s+DATE/TIME[^>]*>([^<]*(\d{2}/\d{2}/\d{4}\s+\d{1,2}:\d{2}(?:am|pm))[^<]*)",
                @"ENTRY\s+DATE/TIME[^>]*>\s*(\d{2}/\d{2}/\d{4}\s+\d{1,2}:\d{2}(?:am|pm))",
                @"ENTRY\s+DATE/TIME.*?(\d{2}/\d{2}/\d{4}\s+\d{1,2}:\d{2}(?:am|pm))"
            };
            
            foreach (var pattern in entryDatePatterns)
            {
                var entryDateMatch = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (entryDateMatch.Success)
                {
                    var dateText = entryDateMatch.Groups.Count > 2 ? entryDateMatch.Groups[2].Value : entryDateMatch.Groups[1].Value;
                    if (DateTime.TryParse(dateText.Trim(), out var parsedEntryDate))
                    {
                        entryDate = parsedEntryDate;
                        _logger.LogDebug("Found entry date: {EntryDate} using pattern: {Pattern}", entryDate, pattern);
                        break;
                    }
                }
            }
            
            // Extract Exit Date/Time with more flexible patterns
            DateTime? exitDate = null;
            var exitDatePatterns = new[]
            {
                @"EXIT\s+DATE/TIME[^>]*>([^<]*(\d{2}/\d{2}/\d{4}\s+\d{1,2}:\d{2}(?:am|pm))[^<]*)",
                @"EXIT\s+DATE/TIME[^>]*>\s*(\d{2}/\d{2}/\d{4}\s+\d{1,2}:\d{2}(?:am|pm))",
                @"EXIT\s+DATE/TIME.*?(\d{2}/\d{2}/\d{4}\s+\d{1,2}:\d{2}(?:am|pm))"
            };
            
            foreach (var pattern in exitDatePatterns)
            {
                var exitDateMatch = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (exitDateMatch.Success)
                {
                    var dateText = exitDateMatch.Groups.Count > 2 ? exitDateMatch.Groups[2].Value : exitDateMatch.Groups[1].Value;
                    if (DateTime.TryParse(dateText.Trim(), out var parsedExitDate))
                    {
                        exitDate = parsedExitDate;
                        _logger.LogDebug("Found exit date: {ExitDate} using pattern: {Pattern}", exitDate, pattern);
                        break;
                    }
                }
            }
            
            // Extract Issued Date as fallback when Entry/Exit dates are not available
            DateTime? issuedDate = null;
            var issuedDatePattern = @"<div\s+class=""label"">Issued\s+Date</div>\s*<div\s+class=""detail"">([^<]+)</div>";
            var issuedDateMatch = Regex.Match(html, issuedDatePattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (issuedDateMatch.Success && DateTime.TryParse(issuedDateMatch.Groups[1].Value.Trim(), out var parsedIssuedDate))
            {
                issuedDate = parsedIssuedDate;
                _logger.LogDebug("Found issued date: {IssuedDate}", issuedDate);
            }
            
            // Extract location - pattern based on actual HTML structure
            var location = "";
            var locationPattern = @"<div\s+class=""label"">Location</div>\s*<div\s+class=""detail"">(.*?)</div>";
            var locationMatch = Regex.Match(html, locationPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (locationMatch.Success)
            {
                var rawLocation = locationMatch.Groups[1].Value;
                
                // Clean up the location text - remove HTML tags and normalize whitespace
                rawLocation = Regex.Replace(rawLocation, @"<br\s*/?>", " ", RegexOptions.IgnoreCase); // Replace <br> with space
                rawLocation = Regex.Replace(rawLocation, @"<[^>]*>", " "); // Remove other HTML tags
                rawLocation = Regex.Replace(rawLocation, @"\s+", " "); // Normalize whitespace
                location = rawLocation.Trim();
                
                _logger.LogDebug("Found location: {Location}", location);
            }
            
            // Extract notice type (violation type) - pattern based on actual HTML structure
            var violationType = "";
            var violationPattern = @"<div\s+class=""label"">Notice\s+Type</div>\s*<div\s+class=""detail"">([^<]+)</div>";
            var violationMatch = Regex.Match(html, violationPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (violationMatch.Success)
            {
                violationType = violationMatch.Groups[1].Value.Trim();
                _logger.LogDebug("Found violation type: {ViolationType}", violationType);
            }
            
            // Determine the best issue date: Exit Date > Issued Date > Entry Date
            var issueDate = exitDate ?? issuedDate ?? entryDate;
            
            var citation = new CitationModel
            {
                NoticeNumber = noticeNumber,
                IssueDate = issueDate,
                Amount = amount,
                Agency = _providerName,
                Tag = licensePlate,
                State = state,
                Currency = "USD",
                PaymentStatus = (int)PaymentStatus.New,
                FineType = (int)FineType.Parking,
                IsActive = true,
                Link = Link,
                CitationProviderType = SupportedProviderType,
                Address = location,
                Note = violationType,
                StartDate = entryDate,
                EndDate = exitDate
            };
            
            _logger.LogDebug("Successfully parsed single notice: {NoticeNumber}, Amount: {Amount}, Location: {Location}", 
                citation.NoticeNumber, citation.Amount, citation.Address);
            
            return citation;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error parsing single notice format");
            return null;
        }
    }
    
    private CitationModel? ExtractCitationFromContext(string context, string noticeNumber, string licensePlate, string state)
    {
        try
        {
            // Extract amount - look for Total amount first
            decimal amount = 0;
            var amountPatterns = new[]
            {
                @"id=[""']paym\d+[""'][^>]*>\$(\d+\.\d{2})",  // id="paym1">$88.99
                @"Total[^>]*>\$(\d+\.\d{2})",               // Total>$88.99
                @"\$(\d+\.\d{2})</span>",                   // $88.99</span>
                @"\$(\d+\.\d{2})"                           // $88.99 anywhere
            };
            
            foreach (var pattern in amountPatterns)
            {
                var amountMatch = Regex.Match(context, pattern, RegexOptions.IgnoreCase);
                if (amountMatch.Success && decimal.TryParse(amountMatch.Groups[1].Value, out amount))
                {
                    _logger.LogDebug("Found amount {Amount} for notice {NoticeNumber} using pattern: {Pattern}", 
                        amount, noticeNumber, pattern);
                    break;
                }
            }
            
            // Extract date/time - prioritize Exit Date/Time from details panel, then main date field
            DateTime? issueDate = null;
            DateTime? entryDate = null;
            DateTime? exitDate = null;
            
            // First, try to extract Entry and Exit dates from the details panel
            var entryDatePattern = @"<div\s+class=""violation-detail-title""[^>]*>Entry\s+Date/Time</div>\s*<div\s+class=""violation-detail-info"">([^<]+)</div>";
            var exitDatePattern = @"<div\s+class=""violation-detail-title""[^>]*>Exit\s+Date/Time</div>\s*<div\s+class=""violation-detail-info"">([^<]+)</div>";
            
            var entryMatch = Regex.Match(context, entryDatePattern, RegexOptions.IgnoreCase);
            if (entryMatch.Success && DateTime.TryParse(entryMatch.Groups[1].Value.Trim(), out var parsedEntryDate))
            {
                entryDate = parsedEntryDate;
                _logger.LogDebug("Found entry date {EntryDate} for notice {NoticeNumber}", entryDate, noticeNumber);
            }
            
            var exitMatch = Regex.Match(context, exitDatePattern, RegexOptions.IgnoreCase);
            if (exitMatch.Success && DateTime.TryParse(exitMatch.Groups[1].Value.Trim(), out var parsedExitDate))
            {
                exitDate = parsedExitDate;
                _logger.LogDebug("Found exit date {ExitDate} for notice {NoticeNumber}", exitDate, noticeNumber);
            }
            
            // Use Exit Date as the primary issue date, or fall back to main date patterns
            if (exitDate.HasValue)
            {
                issueDate = exitDate;
                _logger.LogDebug("Using exit date as issue date for notice {NoticeNumber}", noticeNumber);
            }
            else
            {
                // Fallback to main date field patterns
                var datePatterns = new[]
                {
                    @"(\d{2}/\d{2}/\d{4}\s+\d{1,2}:\d{2}(?:am|pm))",  // 06/11/2021 10:46pm
                    @"(\d{2}/\d{2}/\d{4})"                             // 06/11/2021
                };
                
                foreach (var pattern in datePatterns)
                {
                    var dateMatch = Regex.Match(context, pattern, RegexOptions.IgnoreCase);
                    if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var parsedDate))
                    {
                        issueDate = parsedDate;
                        _logger.LogDebug("Found main date {Date} for notice {NoticeNumber}", issueDate, noticeNumber);
                        break;
                    }
                }
            }
            
            // Extract address - look for address in pay-multiple-reason class
            var location = "";
            var addressPattern = @"<p\s+class=""pay-multiple-reason""[^>]*>([^<]+)</p>";
            var addressMatch = Regex.Match(context, addressPattern, RegexOptions.IgnoreCase);
            
            if (addressMatch.Success)
            {
                var rawAddress = addressMatch.Groups[1].Value.Trim();
                // Clean up whitespace and normalize
                location = Regex.Replace(rawAddress, @"\s+", " ");
                _logger.LogDebug("Found address {Address} for notice {NoticeNumber} from pay-multiple-reason", location, noticeNumber);
            }
            else
            {
                // Fallback to original patterns if pay-multiple-reason not found
                var fallbackPatterns = new[]
                {
                    // Full address with state and zip - more flexible
                    @"(\d+\s+[A-Z0-9\s]+(?:AVE|AVENUE|ST|STREET|RD|ROAD|BLVD|BOULEVARD|DR|DRIVE|LN|LANE|CT|COURT|PL|PLACE|WAY)\s+[A-Z\s]+,?\s*[A-Z]{2}\s+\d{5})",
                    // Address without zip - more flexible
                    @"(\d+\s+[A-Z0-9\s]+(?:AVE|AVENUE|ST|STREET|RD|ROAD|BLVD|BOULEVARD|DR|DRIVE|LN|LANE|CT|COURT|PL|PLACE|WAY)\s+[A-Z\s]+)",
                    // Simple street address - more flexible
                    @"(\d+\s+[A-Z0-9\s]+(?:AVE|AVENUE|ST|STREET|RD|ROAD|BLVD|BOULEVARD|DR|DRIVE|LN|LANE|CT|COURT|PL|PLACE|WAY))",
                    // Specific patterns for the problematic addresses
                    @"(\d+\s+[A-Z]+\s+\d+(?:ST|ND|RD|TH)\s+(?:AVE|ST|STREET|AVENUE)\s+[A-Z\s]+,?\s*[A-Z]{2}\s+\d{5})", // 2060 NE 2ND ST DEERFIELD BCH, FL 33441
                    @"(\d+\s+[A-Z]+\s+\d+(?:ST|ND|RD|TH)\s+(?:AVE|ST|STREET|AVENUE)\s+[A-Z\s]+)" // 710 SW 16TH AVE MIAMI
                };
                
                foreach (var pattern in fallbackPatterns)
                {
                    var fallbackMatch = Regex.Match(context, pattern, RegexOptions.IgnoreCase);
                    if (fallbackMatch.Success)
                    {
                        var potentialLocation = fallbackMatch.Groups[1].Value.Trim();
                        potentialLocation = Regex.Replace(potentialLocation, @"\s+", " ");
                        
                        // Filter out contamination
                        var contaminationTerms = new[] { "Non Payment", "Paid", "Total", "Charge", "Conv", "Fee" };
                        var isClean = !contaminationTerms.Any(term => 
                            potentialLocation.Contains(term, StringComparison.OrdinalIgnoreCase));
                        
                        if (isClean && potentialLocation.Length > 10)
                        {
                            location = potentialLocation;
                            _logger.LogDebug("Found address {Address} for notice {NoticeNumber} using fallback pattern: {Pattern}", location, noticeNumber, pattern);
                            break;
                        }
                    }
                }
            }
            
            // Extract violation type - look for violation type in pay-multiple-reason detail class
            var violationType = "";
            var violationPattern = @"<div\s+class=""pay-multiple-reason\s+detail\s+epy111""[^>]*>([^<]+)</div>";
            var violationMatch = Regex.Match(context, violationPattern, RegexOptions.IgnoreCase);
            
            if (violationMatch.Success)
            {
                violationType = violationMatch.Groups[1].Value.Trim();
                _logger.LogDebug("Found violation type {ViolationType} for notice {NoticeNumber} from pay-multiple-reason detail", violationType, noticeNumber);
            }
            else
            {
                // Fallback to original patterns if pay-multiple-reason detail not found
                var fallbackPatterns = new[] { "Non Payment", "Expired Meter", "No Permit", "Overtime Parking", "Failure to Register", "Overstay" };
                foreach (var pattern in fallbackPatterns)
                {
                    if (context.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        violationType = pattern;
                        _logger.LogDebug("Found violation type {ViolationType} for notice {NoticeNumber} using fallback", violationType, noticeNumber);
                        break;
                    }
                }
            }
            
            var citation = new CitationModel
            {
                NoticeNumber = noticeNumber,
                CitationNumber = noticeNumber,
                IssueDate = issueDate,
                Amount = amount,
                Agency = _providerName,
                Tag = licensePlate,
                State = state,
                Currency = "USD",
                PaymentStatus = (int)PaymentStatus.New,
                FineType = (int)FineType.Parking,
                IsActive = true,
                Link = Link,
                CitationProviderType = SupportedProviderType,
                Address = location,
                Note = violationType,
                StartDate = entryDate,
                EndDate = exitDate
            };
            
            return citation;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error extracting citation data from context for notice {NoticeNumber}", noticeNumber);
            return null;
        }
    }

    private async Task<BaseResponse<string>> GetPageAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            request.Headers.Clear();
            request.Headers.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("GET request response status: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                return BaseResponse<string>.Success(content);
            }
            
            var errorMessage = $"HTTP {(int)response.StatusCode}: GET request failed";
            return BaseResponse<string>.Failure((int)response.StatusCode, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during GET request");
            return BaseResponse<string>.Failure(-1, $"GET request exception: {ex.Message}");
        }
    }

    private async Task<BaseResponse<string>> SubmitFormAsync(string url, FormUrlEncodedContent formContent)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            request.Headers.Clear();
            request.Headers.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Cache-Control", "max-age=0");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            
            var baseUri = new Uri(url);
            request.Headers.Add("Origin", $"{baseUri.Scheme}://{baseUri.Host}");
            request.Headers.Add("Referer", url);
            
            request.Content = formContent;
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            _logger.LogDebug("Form submission response status: {StatusCode}", response.StatusCode);
            
            if (content.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase) && 
                content.Contains("Token not provided", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Server returned JSON error about missing token, treating as no results");
                return BaseResponse<string>.Failure(400, "Token not provided");
            }
            
            if (response.IsSuccessStatusCode)
            {
                return BaseResponse<string>.Success(content);
            }
            
            var errorMessage = $"HTTP {(int)response.StatusCode}: Form submission failed";
            return BaseResponse<string>.Failure((int)response.StatusCode, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during form submission");
            return BaseResponse<string>.Failure(-1, $"Form submission exception: {ex.Message}");
        }
    }

    private void ExtractAndAddTokens(string html, List<KeyValuePair<string, string>> formData)
    {
        try
        {
            var tokenPatterns = new[]
            {
                (@"<input[^>]*name=['""]_token['""][^>]*value=['""]([^'""]*)['""]", "_token"),
                (@"<input[^>]*name=['""]csrf_token['""][^>]*value=['""]([^'""]*)['""]", "csrf_token"),
                (@"<meta[^>]*name=['""]csrf-token['""][^>]*content=['""]([^'""]*)['""]", "_token")
            };
            
            foreach (var (pattern, tokenName) in tokenPatterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var tokenValue = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(tokenValue))
                    {
                        formData.Add(new KeyValuePair<string, string>("_token", tokenValue));
                        _logger.LogDebug("Found token: {TokenName} = {TokenValue}", tokenName, tokenValue[..Math.Min(10, tokenValue.Length)] + "...");
                        return; // Only add one token
                    }
                }
            }
            
            _logger.LogWarning("No tokens found in HTML");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting tokens from HTML");
        }
    }
    
    private string ExtractFormActionUrl(string html)
    {
        try
        {
            _logger.LogDebug("Extracting form action URL from HTML content");
            
            // Try multiple form patterns to find the search form
            var formPatterns = new[]
            {
                // Standard form with action attribute
                @"<form[^>]*action=['""]([^'""]*)['""][^>]*>",
                @"<form[^>]*action=([^'""\s>]+)[^>]*>",
                
                // Form with method POST (likely search form)
                @"<form[^>]*method=['""]post['""][^>]*action=['""]([^'""]*)['""][^>]*>",
                @"<form[^>]*action=['""]([^'""]*)['""][^>]*method=['""]post['""][^>]*>"
            };
            
            string? foundActionUrl = null;
            
            foreach (var pattern in formPatterns)
            {
                var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                    {
                        foundActionUrl = match.Groups[1].Value;
                        _logger.LogDebug("Found form action URL: {ActionUrl}", foundActionUrl);
                        break;
                    }
                }
                
                if (!string.IsNullOrEmpty(foundActionUrl))
                    break;
            }
            
            if (!string.IsNullOrEmpty(foundActionUrl))
            {
                // Process the found action URL
                if (string.IsNullOrEmpty(foundActionUrl) || foundActionUrl == "#")
                {
                    // Empty or # action means submit to current page
                    _logger.LogDebug("Form action is empty or #, using base URL");
                    return BaseUrl;
                }
                
                // If it's a relative URL starting with /, make it absolute
                if (foundActionUrl.StartsWith("/"))
                {
                    var baseUri = new Uri(BaseUrl);
                    var absoluteUrl = $"{baseUri.Scheme}://{baseUri.Host}{foundActionUrl}";
                    _logger.LogDebug("Converted relative URL {RelativeUrl} to absolute {AbsoluteUrl}", foundActionUrl, absoluteUrl);
                    return absoluteUrl;
                }
                
                // If it's already absolute, return as-is
                if (foundActionUrl.StartsWith("http"))
                {
                    _logger.LogDebug("Using absolute URL: {AbsoluteUrl}", foundActionUrl);
                    return foundActionUrl;
                }
                
                // If it's a relative path without leading slash
                var baseUriForRelative = new Uri(BaseUrl);
                var resolvedUrl = new Uri(baseUriForRelative, foundActionUrl).ToString();
                _logger.LogDebug("Resolved relative URL {RelativeUrl} to {ResolvedUrl}", foundActionUrl, resolvedUrl);
                return resolvedUrl;
            }
            
            // If no form action found at all, fall back to base URL
            _logger.LogWarning("No form action found in HTML, using base URL for form submission");
            return BaseUrl;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting form action URL, using base URL");
            return BaseUrl;
        }
    }
    
    private string? ExtractRedirectLocation(string jsonResponse)
    {
        try
        {
            const string locationPattern = @"""location""\s*:\s*""([^""]*)""";
            var match = Regex.Match(jsonResponse, locationPattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var location = match.Groups[1].Value.Replace("\\/", "/");
                
                if (location.StartsWith("/"))
                {
                    var baseUri = new Uri(BaseUrl);
                    return $"{baseUri.Scheme}://{baseUri.Host}{location}";
                }
                
                if (location.StartsWith("http"))
                {
                    return location;
                }
                
                var baseUriForRelative = new Uri(BaseUrl);
                return new Uri(baseUriForRelative, location).ToString();
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting redirect location from JSON");
            return null;
        }
    }
}
