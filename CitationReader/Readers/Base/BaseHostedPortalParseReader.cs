using System.Net;
using System.Text.RegularExpressions;
using CitationReader.Enums;
using CitationReader.Models.Base;
using CitationReader.Models.Huur;
using HtmlAgilityPack;

namespace CitationReader.Readers.Base;

public class BaseHostedPortalParseReader : IDisposable
{
    private static readonly Dictionary<string, string> StateToIdMap = new()
    {
        { "AL", "1" },  { "AK", "2" },  { "AZ", "3" },  { "AR", "4" },  { "CA", "5" },
        { "CO", "6" },  { "CT", "7" },  { "DE", "8" },  { "DC", "9" },  { "FL", "10" },
        { "GA", "11" }, { "HI", "12" }, { "ID", "13" }, { "IL", "14" }, { "IN", "15" },
        { "IA", "16" }, { "KS", "17" }, { "KY", "18" }, { "LA", "19" }, { "ME", "20" },
        { "MD", "21" }, { "MA", "22" }, { "MI", "23" }, { "MN", "24" }, { "MS", "25" },
        { "MO", "26" }, { "MT", "27" }, { "NE", "28" }, { "NV", "29" }, { "NH", "30" },
        { "NJ", "31" }, { "NM", "32" }, { "NY", "33" }, { "NC", "34" }, { "ND", "35" },
        { "OH", "36" }, { "OK", "37" }, { "OR", "38" }, { "PA", "39" }, { "RI", "40" },
        { "SC", "41" }, { "SD", "42" }, { "TN", "43" }, { "TX", "44" }, { "UT", "45" },
        { "VT", "46" }, { "VA", "47" }, { "WA", "48" }, { "WV", "49" }, { "WI", "50" },
        { "WY", "51" }
    };

    private const int MaxRetries = 3;
    private const int RetryDelayMs = 1500;

    private readonly string _baseUrl;
    private readonly string _portalUrl;
    private readonly string _searchUrl;
    private readonly string _agency;
    private bool _disposed;
    private HttpClient _httpClient;
    
    protected readonly ILogger Logger;

    public BaseHostedPortalParseReader(string baseUrl, string agency)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("Base URL cannot be null or empty", nameof(baseUrl));
        }

        if (string.IsNullOrWhiteSpace(agency))
        {
            throw new ArgumentException("Agency cannot be null or empty", nameof(agency));
        }
        
        Logger = Program.ServiceProvider.GetService<ILogger<BaseHostedPortalParseReader>>()!;
        
        _baseUrl = baseUrl.TrimEnd('/');
        _portalUrl = $"{_baseUrl}/Account/Portal";
        _searchUrl = $"{_baseUrl}/Account/Citations/Search";
        _agency = agency;
    }

    protected async Task<BaseResponse<List<ParkingViolation>>> SearchCitationAsync(string plateNumber, string stateCode)
    {
        var httpClientFactory = Program.ServiceProvider.GetService<IHttpClientFactory>()!;
        _httpClient = httpClientFactory.CreateClient(HttpClientType.ParseHostedCitationReader.ToString());
        
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            return BaseResponse<List<ParkingViolation>>.Failure(400, "License plate number cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(stateCode))
        {
            return BaseResponse<List<ParkingViolation>>.Failure(400, "State code cannot be empty");
        }

        var carDetails = $"{plateNumber.ToUpper()} ({stateCode.ToUpper()})";
        
        try
        {
            Logger.LogInformation("Starting citation search for {CarDetails}", carDetails);

            // Set headers
            SetFormSubmissionHeaders();
            
            // Step 1: Simulate full browser session - start with home page
            await SimulateBrowserSession();
            
            // Step 2: Load portal page to get cookies and token
            var portalResponse = await GetPageWithRetryAsync(_portalUrl);
            if (!portalResponse.IsSuccess)
            {
                Logger.LogWarning("Failed to load portal page for {CarDetails}: {Error}", carDetails, portalResponse.Message);
                return BaseResponse<List<ParkingViolation>>.Failure(portalResponse.Reason, 
                    portalResponse.Message ?? "Failed to load portal page");
            }

            // Step 3: Parse verification token
            var token = ExtractVerificationToken(portalResponse.Result);
            if (string.IsNullOrEmpty(token))
            {
                Logger.LogWarning("Could not extract verification token for {CarDetails}", carDetails);
                // Continue without token - some sites might work without it
            }
            else
            {
                Logger.LogDebug("Successfully extracted verification token for {CarDetails}", carDetails);
            }

            // Step 4: Convert state code to ID
            if (!TryGetStateId(stateCode, out var stateId))
            {
                Logger.LogWarning("Invalid state code provided: {StateCode}", stateCode);
                return BaseResponse<List<ParkingViolation>>.Failure(400, $"Invalid state code: {stateCode}");
            }

            Logger.LogDebug("Converted state {StateCode} to ID {StateId}", stateCode, stateId);

            // Step 5: Submit search with retry logic
            var searchResponse = await SubmitSearchWithRetryAsync(plateNumber, stateId, token, carDetails);
            if (!searchResponse.IsSuccess)
            {
                return BaseResponse<List<ParkingViolation>>.Failure(searchResponse.Reason, searchResponse.Message);
            }

            var resultPath = GetResultPath(searchResponse.Result);
            if (string.IsNullOrEmpty(resultPath))
            {
                Logger.LogInformation("No citations found for {CarDetails}", carDetails);
                return BaseResponse<List<ParkingViolation>>.Success(new List<ParkingViolation>());
            }

            // Step 6: Get and parse results with retry logic for ASP.NET errors
            var resultUrl = $"{_baseUrl}{resultPath}";
            
            var resultResponse = await GetResultsWithRetryAsync(resultUrl, carDetails);
            if (!resultResponse.IsSuccess)
            {
                return BaseResponse<List<ParkingViolation>>.Failure(resultResponse.Reason, resultResponse.Message);
            }
            
            var resultHtml = resultResponse.Result;
         
            var citations = ParseCitations(resultHtml, plateNumber, stateCode);
            Logger.LogInformation("Found {CitationCount} citations for {CarDetails}", citations.Count, carDetails);
            
            return BaseResponse<List<ParkingViolation>>.Success(citations);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception occurred while searching citations for {CarDetails}", carDetails);
            return BaseResponse<List<ParkingViolation>>.Failure(-1, $"Exception occurred: {ex.Message}");
        }
    }

    private static bool TryGetStateId(string stateCode, out string stateId)
    {
        stateId = string.Empty;
        return !string.IsNullOrWhiteSpace(stateCode) && StateToIdMap.TryGetValue(stateCode.ToUpper(), out stateId);
    }

    private async Task<BaseResponse<string>> GetPageWithRetryAsync(string url)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                Logger.LogDebug("Making GET request to {Url} (attempt {Attempt}/{MaxRetries})", url, attempt + 1, MaxRetries);
                
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Logger.LogDebug("Successfully retrieved page from {Url}", url);
                    return BaseResponse<string>.Success(content);
                }

                var errorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                Logger.LogWarning("GET request failed with status {StatusCode} for {Url}", response.StatusCode, url);

                if (attempt >= MaxRetries - 1)
                {
                    return BaseResponse<string>.Failure((int)response.StatusCode, errorMessage);
                }

                await Task.Delay(RetryDelayMs * (attempt + 1));
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Logger.LogWarning(
                    ex, 
                    "HTTP request failed on attempt {Attempt}/{MaxRetries} for {Url}, retrying...", 
                    attempt + 1,
                    MaxRetries, url);
                await Task.Delay(RetryDelayMs * (attempt + 1));
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < MaxRetries - 1)
            {
                Logger.LogWarning(
                    ex, 
                    "Request timeout on attempt {Attempt}/{MaxRetries} for {Url}, retrying...", 
                    attempt + 1, 
                    MaxRetries,
                    url);
                await Task.Delay(RetryDelayMs * (attempt + 1));
            }
        }

        return BaseResponse<string>.Failure(-1, "All retry attempts failed");
    }

    private async Task<BaseResponse<string>> SubmitSearchWithRetryAsync(
        string plateNumber,
        string stateId, 
        string? token, 
        string carDetails)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                Logger.LogDebug(
                    "Submitting search form for {CarDetails} (attempt {Attempt}/{MaxRetries})", 
                    carDetails,
                    attempt + 1, 
                    MaxRetries);

                var formData = CreateFormData(plateNumber, stateId, token);
                var content = new FormUrlEncodedContent(formData);

                if (attempt > 0)
                {
                    await Task.Delay(RetryDelayMs * attempt);
                }

                var response = await _httpClient.PostAsync(_searchUrl, content);
                var result = await response.Content.ReadAsStringAsync();

                Logger.LogDebug("Search form submission response status: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    Logger.LogDebug("Successfully submitted search form for {CarDetails}", carDetails);
                    return BaseResponse<string>.Success(result);
                }

                var errorMessage = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                Logger.LogWarning("Form submission failed with status {StatusCode} for {CarDetails}", 
                    response.StatusCode, carDetails);

                if (attempt >= MaxRetries - 1)
                {
                    return BaseResponse<string>.Failure((int)response.StatusCode, errorMessage);
                }
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Logger.LogWarning(
                    ex, 
                    "Form submission failed on attempt {Attempt}/{MaxRetries} for {CarDetails}, retrying...", 
                    attempt + 1,
                    MaxRetries,
                    carDetails);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < MaxRetries - 1)
            {
                Logger.LogWarning(
                    ex, 
                    "Form submission timeout on attempt {Attempt}/{MaxRetries} for {CarDetails}, retrying...", 
                    attempt + 1,
                    MaxRetries, 
                    carDetails);
            }
        }

        return BaseResponse<string>.Failure(-1, "All form submission attempts failed");
    }

    private async Task<BaseResponse<string>> GetResultsWithRetryAsync(string resultUrl, string carDetails)
    {
        for (var attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                Logger.LogDebug("Getting results from {ResultUrl} for {CarDetails} (attempt {Attempt}/{MaxRetries})", 
                    resultUrl, carDetails, attempt + 1, MaxRetries);

                // Add a small delay before each attempt to give the server time to process
                if (attempt > 0)
                {
                    await Task.Delay(RetryDelayMs * attempt);
                }

                var response = await _httpClient.GetAsync(resultUrl);
                var resultHtml = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Logger.LogWarning("Results request failed with status {StatusCode} for {CarDetails}", 
                        response.StatusCode, carDetails);
                    
                    if (attempt >= MaxRetries - 1)
                    {
                        return BaseResponse<string>.Failure((int)response.StatusCode, 
                            $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
                    }
                    continue;
                }

                // Check if we received an ASP.NET error page instead of citation data
                if (IsAspNetErrorPage(resultHtml))
                {
                    Logger.LogWarning("Received ASP.NET error page on attempt {Attempt}/{MaxRetries} for {CarDetails}. URL: {ResultUrl}", 
                        attempt + 1, MaxRetries, carDetails, resultUrl);
                    
                    if (attempt >= MaxRetries - 1)
                    {
                        Logger.LogError("All attempts failed due to ASP.NET errors for {CarDetails}. Final error page length: {Length}", 
                            carDetails, resultHtml.Length);
                        Logger.LogDebug("Final ASP.NET Error page content: {ErrorContent}", 
                            resultHtml.Substring(0, Math.Min(500, resultHtml.Length)));
                        return BaseResponse<string>.Failure(500, 
                            "The citation website is currently experiencing technical difficulties. Please try again later.");
                    }
                    continue;
                }

                Logger.LogDebug("Successfully retrieved results for {CarDetails}", carDetails);
                return BaseResponse<string>.Success(resultHtml);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries - 1)
            {
                Logger.LogWarning(ex, "HTTP request failed on attempt {Attempt}/{MaxRetries} for results {CarDetails}, retrying...", 
                    attempt + 1, MaxRetries, carDetails);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && attempt < MaxRetries - 1)
            {
                Logger.LogWarning(ex, "Request timeout on attempt {Attempt}/{MaxRetries} for results {CarDetails}, retrying...", 
                    attempt + 1, MaxRetries, carDetails);
            }
        }

        return BaseResponse<string>.Failure(-1, "All result retrieval attempts failed");
    }

    private async Task SimulateBrowserSession()
    {
        try
        {
            Logger.LogDebug("Simulating browser session by visiting home page first");
            
            // Visit the home page first to establish a proper session
            var homePageResponse = await _httpClient.GetAsync(_baseUrl);
            if (homePageResponse.IsSuccessStatusCode)
            {
                Logger.LogDebug("Successfully visited home page");
                //TODO: check how works
                //await Task.Delay(1000);
            }
            else
            {
                Logger.LogWarning("Failed to visit home page, status: {StatusCode}", homePageResponse.StatusCode);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error during browser session simulation, continuing anyway");
        }
    }


    private void SetFormSubmissionHeaders()
    {
        _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        _httpClient.DefaultRequestHeaders.Add("Referer", _portalUrl);
        _httpClient.DefaultRequestHeaders.Add("Origin", _baseUrl);
    }

    private static Dictionary<string, string> CreateFormData(string plateNumber, string stateId, string? token)
    {
        var formData = new Dictionary<string, string>
        {
            { "PlateNumber", plateNumber.ToUpper() },
            { "StateId", stateId },
            { "CitationNumber", "" },
            { "X-Requested-With", "XMLHttpRequest" }
        };

        if (!string.IsNullOrEmpty(token))
        {
            formData["__RequestVerificationToken"] = token;
        }

        return formData;
    }

    private static string? ExtractVerificationToken(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tokenNode = doc.DocumentNode.SelectSingleNode("//input[@name='__RequestVerificationToken']");
            return tokenNode?.GetAttributeValue("value", "");
        }
        catch (Exception)
        {
            var match = Regex.Match(
                html, 
                @"<input[^>]*name=['""]__RequestVerificationToken['""][^>]*value=['""]([^'""]*)['""]", 
                RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
    }

    private static string? GetResultPath(string result)
    {
        if (string.IsNullOrEmpty(result))
        {
            return null;
        }

        if (!result.StartsWith("document.location"))
        {
            return null;
        }
        
        var startIndex = result.IndexOf("/", StringComparison.Ordinal);
        if (startIndex >= 0)
        {
            return result[startIndex..].Replace(";", "").Replace("'", "");
        }

        return null;
    }

    private List<ParkingViolation> ParseCitations(
        string html,
        string plateNumber,
        string stateCode)
    {
        var citations = new List<ParkingViolation>();

        if (string.IsNullOrEmpty(html))
        {
            return citations;
        }

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Select all citation rows (skip the header row)
            var citationRows = doc.DocumentNode.SelectNodes("//table[@id='citations-list-table']//tr[starts-with(@id, 'citation')]");
            if (citationRows != null)
            {
                foreach (var row in citationRows)
                {
                    try
                    {
                        var citation = ParseCitationRow(row, plateNumber, stateCode);
                        if (citation != null)
                        {
                            citations.Add(citation);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning(ex, "Error parsing individual citation row");
                        // Continue processing other rows
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing citations from HTML");
        }

        return citations;
    }

    private ParkingViolation? ParseCitationRow(HtmlNode row, string plateNumber, string stateCode)
    {
        var cells = row.SelectNodes(".//td");
        if (cells == null || cells.Count < 6)
        {
            return null;
        }

        try
        {
            var citation = new ParkingViolation
            {
                FineType = (int)FineType.Parking,
                Tag = plateNumber,
                State = stateCode,
                Agency = _agency,
                Link = _baseUrl,
                IsActive = true,
                Currency = "USD"
            };

            // Extract ID from the row id attribute
            var rowId = row.GetAttributeValue("id", "");
            if (!string.IsNullOrEmpty(rowId))
            {
                citation.Note = rowId;
            }

            // Citation Number (cell 0)
            citation.CitationNumber = CleanHtmlText(cells[0].InnerText);

            // Status (cell 1) - map to PaymentStatus
            var status = CleanHtmlText(cells[1].InnerText);
            citation.PaymentStatus = (int)GetStatus(status);

            // Amount (cell 2) - parse dollar amount
            var balanceText = CleanHtmlText(cells[2].InnerText);
            citation.Amount = ParseAmount(balanceText);

            // Issue Date (cell 3)
            var dateText = CleanHtmlText(cells[3].InnerText);
            citation.IssueDate = ParseDateTime(dateText);

            // Location / Address (cell 5)
            citation.Address = CleanHtmlText(cells[5].InnerText);

            return citation;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error parsing citation row data");
            return null;
        }
    }

    private static string CleanHtmlText(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = Regex.Replace(html, @"<[^>]+>", "");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static decimal ParseAmount(string amountText)
    {
        if (string.IsNullOrEmpty(amountText))
        {
            return 0;
        }

        var numericText = Regex.Replace(amountText, @"[^\d.]", "");
        return decimal.TryParse(numericText, out var amount) ? amount : 0;
    }

    private static DateTime ParseDateTime(string dateText)
    {
        if (string.IsNullOrEmpty(dateText))
        {
            return DateTime.MinValue;
        }

        return DateTime.TryParse(dateText, out var date) ? date : DateTime.MinValue;
    }

    private static PaymentStatus GetStatus(string? status)
    {
        if (string.IsNullOrEmpty(status))
        {
            return PaymentStatus.Unknown;
        }

        return status.ToUpper() switch
        {
            "OPEN" or "UNPAID" => PaymentStatus.New,
            "PAID" => PaymentStatus.Paid,
            "VOID" => PaymentStatus.Paid,
            "PENDING" => PaymentStatus.Paid,
            "OVERDUE" => PaymentStatus.New,
            "CLOSED VOID" => PaymentStatus.Paid,
            "CLOSED WARNING" => PaymentStatus.Paid,
            "CLOSED PAID" => PaymentStatus.Paid,
            _ => PaymentStatus.Unknown
        };
    }

    private static bool IsAspNetErrorPage(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return false;
        }

        // Check for common ASP.NET error page indicators
        var errorIndicators = new[]
        {
            "An application error occurred on the server",
            "Description: An application error occurred",
            "current custom error settings for this application prevent the details",
            "customErrors",
            "web.config",
            "Server Error in",
            "Runtime Error"
        };

        return errorIndicators.Any(indicator => 
            html.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}
