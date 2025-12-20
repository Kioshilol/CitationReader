using CitationReader.Enums;
using CitationReader.Models.Base;
using CitationReader.Models.Citation.Internal;
using System.Text.RegularExpressions;

namespace CitationReader.Readers.Base;

public abstract class BaseParseReader
{
    private readonly HttpClient _httpClient;
    protected readonly ILogger Logger;

    protected BaseParseReader(HttpClientType httpClientType)
    {
        var httpClientFactory = Program.ServiceProvider.GetService<IHttpClientFactory>()!;
        Logger = Program.ServiceProvider.GetService<ILogger<BaseParseReader>>()!;
        
        _httpClient = httpClientFactory.CreateClient(httpClientType.ToString());
    }

    public abstract CitationProviderType SupportedProviderType { get; }
    public abstract string Link { get; }
    protected abstract string BaseUrl { get; }
    protected abstract string ProviderName { get; }

    public virtual async Task<BaseCitationResult<IEnumerable<CitationModel>>> ReadCitationsAsync(
        string licensePlate,
        string state)
    {
        state = state.ToUpper();
        
        var carDetails = $"{licensePlate} ({state})";
        
        try
        {
            Logger.LogInformation("Starting citation search for {CarDetails} using form submission", carDetails);
            
            // Step 1: Get the initial form page
            var initialResponse = await GetPageAsync(BaseUrl);
            if (!initialResponse.IsSuccess)
            {
                Logger.LogWarning("Failed to load initial form page: {Error}", initialResponse.Message);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateError(
                    "Failed to load search form",
                    SupportedProviderType,
                    carDetails,
                    state,
                    initialResponse.Reason);
            }

            // Step 2: Extract any required tokens and form action URL from the page
            var pageContent = initialResponse.Result;
            var formData = CreateFormData(licensePlate);
            
            // Look for various types of tokens
            if (!string.IsNullOrEmpty(pageContent))
            {
                ExtractAndAddTokens(pageContent, formData);
            }
            else
            {
                Logger.LogWarning("Page content is empty - cannot extract tokens");
            }
            
            // Log all form data being sent
            Logger.LogInformation("Form data being submitted:");
            foreach (var kvp in formData)
            {
                Logger.LogInformation("  {Key} = {Value}", kvp.Key, kvp.Value.Length > 50 ? kvp.Value[..50] + "..." : kvp.Value);
            }
            
            var formActionUrl = ExtractFormActionUrl(pageContent, BaseUrl);
            var formContent = new FormUrlEncodedContent(formData);
            
            // Add a small delay to mimic human behavior
            await Task.Delay(1500);
            
            // Try form submission first
            var submitResponse = await SubmitFormAsync(formActionUrl, formContent);
            if (!submitResponse.IsSuccess && 
                submitResponse.Message?.Contains("Token not provided", StringComparison.OrdinalIgnoreCase) == true)
            {
                Logger.LogInformation("Token error detected - treating as no citations found for {CarDetails}", carDetails);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                    ArraySegment<CitationModel>.Empty,
                    state);
            }
            
            if (!submitResponse.IsSuccess)
            {
                Logger.LogWarning("Form submission failed for {CarDetails}: {Error}", carDetails, submitResponse.Message);
                
                // Check if this is a "no results" case vs actual error
                if (submitResponse.Message?.Contains("no citations", StringComparison.OrdinalIgnoreCase) == true ||
                    submitResponse.Message?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Logger.LogInformation("No citations found for {CarDetails}", carDetails);
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

            // Step 3: Check if response is JSON with redirect location
            var responseContent = submitResponse.Result;
            string? citationPageHtml = null;
            
            if (responseContent.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase) && 
                responseContent.Contains("\"location\":", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Form submission returned JSON redirect response");
                
                // Parse JSON to extract location
                var redirectLocation = ExtractRedirectLocation(responseContent, BaseUrl);
                if (!string.IsNullOrEmpty(redirectLocation))
                {
                    Logger.LogInformation("Following redirect to citation page: {Location}", redirectLocation);
                    
                    // Get the actual citation page
                    var citationPageResponse = await GetPageAsync(redirectLocation);
                    if (citationPageResponse.IsSuccess)
                    {
                        citationPageHtml = citationPageResponse.Result;
                        Logger.LogInformation("Successfully retrieved citation page HTML");
                    }
                    else
                    {
                        Logger.LogWarning("Failed to retrieve citation page: {Error}", citationPageResponse.Message);
                        return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                            ArraySegment<CitationModel>.Empty,
                            state);
                    }
                }
                else
                {
                    Logger.LogWarning("Could not extract redirect location from JSON response");
                    return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                        ArraySegment<CitationModel>.Empty,
                        state);
                }
            }
            else
            {
                // Direct HTML response
                citationPageHtml = responseContent;
                Logger.LogInformation("Form submission returned direct HTML response");
            }

            // Step 4: Parse the citation page HTML
            var citations = ParseCitationsFromHtml(citationPageHtml, licensePlate, state);
            if (!citations.Any())
            {
                Logger.LogInformation("No citations found for vehicle: {CarDetails}", carDetails);
                return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(
                    ArraySegment<CitationModel>.Empty,
                    state);
            }
            
            Logger.LogInformation("Found {Count} citations for vehicle: {CarDetails}", citations.Count, carDetails);
            
            return BaseCitationResult<IEnumerable<CitationModel>>.CreateSuccess(citations, state);
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

    protected abstract string GetLicensePlateFieldName();
    protected abstract string GetStateFieldName();
    protected abstract string[] GetNoResultsIndicators();
    protected abstract string[] GetCitationNumberPatterns();
    protected abstract List<CitationModel> ParseProviderSpecificFormat(string html, string licensePlate, string state);
    protected abstract string GetTokenFieldName();
    
    protected virtual List<CitationModel> ParseCitationsFromHtml(string? html, string licensePlate, string state)
    {
        var citations = new List<CitationModel>();
        
        if (string.IsNullOrEmpty(html))
        {
            return citations;
        }
        
        try
        {
            // Check if page indicates no results
            var noResultsIndicators = GetNoResultsIndicators();
            if (noResultsIndicators.Any(indicator => html.Contains(indicator, StringComparison.OrdinalIgnoreCase)))
            {
                return citations;
            }
            
            // Try provider-specific parsing first
            var providerCitations = ParseProviderSpecificFormat(html, licensePlate, state);
            if (providerCitations.Any())
            {
                citations.AddRange(providerCitations);
                Logger.LogInformation("Successfully parsed {Count} citations using provider-specific format", providerCitations.Count);
                return citations;
            }
            
            // Fallback to table-based parsing for generic formats
            var citationPattern = @"<tr[^>]*>.*?</tr>";
            var matches = Regex.Matches(html, citationPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            foreach (Match match in matches)
            {
                var rowHtml = match.Value;
                
                // Skip header rows
                if (rowHtml.Contains("<th", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                // Extract citation data from table row
                var citation = ExtractCitationFromTableRow(rowHtml, licensePlate, state);
                if (citation != null)
                {
                    citations.Add(citation);
                }
            }
            
            // If no table structure found, try alternative parsing methods
            if (!citations.Any())
            {
                citations.AddRange(ParseAlternativeFormats(html, licensePlate, state));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error parsing citations from HTML");
        }
        
        return citations;
    }
    
    protected virtual CitationModel? ExtractCitationFromTableRow(string rowHtml, string licensePlate, string state)
    {
        try
        {
            // Extract cell values from table row
            var cellPattern = @"<td[^>]*>(.*?)</td>";
            var cellMatches = Regex.Matches(rowHtml, cellPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            if (cellMatches.Count < 3) // Need at least a few cells for valid citation
                return null;
            
            var cells = cellMatches.Cast<Match>()
                .Select(m => CleanHtmlText(m.Groups[1].Value))
                .ToArray();
            
            // Map cells to citation properties (adjust indices based on actual table structure)
            var citation = new CitationModel
            {
                NoticeNumber = cells.Length > 0 ? cells[0] : "",
                CitationNumber = cells.Length > 1 ? cells[1] : "",
                IssueDate = cells.Length > 2 ? ParseDateTime(cells[2]) : DateTime.MinValue,
                Amount = cells.Length > 3 ? ParseAmount(cells[3]) : 0,
                Agency = ProviderName,
                Tag = licensePlate,
                State = state,
                Currency = "USD",
                PaymentStatus = DeterminePaymentStatus(cells),
                FineType = (int)FineType.Parking,
                IsActive = DetermineIsActive(cells),
                Link = Link,
                CitationProviderType = SupportedProviderType
            };
            
            // Only return if we have essential data
            if (!string.IsNullOrEmpty(citation.NoticeNumber) || !string.IsNullOrEmpty(citation.CitationNumber))
            {
                return citation;
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Error extracting citation from table row");
        }
        
        return null;
    }
    
    protected virtual List<CitationModel> ParseAlternativeFormats(string html, string licensePlate, string state)
    {
        var citations = new List<CitationModel>();
        
        // Try to find citation numbers in various formats
        var citationNumberPatterns = GetCitationNumberPatterns();
        
        foreach (var pattern in citationNumberPatterns)
        {
            var matches = Regex.Matches(html, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var citationNumber = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(citationNumber))
                {
                    citations.Add(new CitationModel
                    {
                        CitationNumber = citationNumber,
                        NoticeNumber = citationNumber,
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
        }
        
        return citations;
    }
    
    protected virtual int DeterminePaymentStatus(string[] cells)
    {
        var allText = string.Join(" ", cells).ToLower();
        if (allText.Contains("paid") || allText.Contains("settled"))
        {
            return (int)PaymentStatus.Paid;
        }
        
        return (int)PaymentStatus.New;
    }
    
    protected virtual bool DetermineIsActive(string[] cells)
    {
        var allText = string.Join(" ", cells).ToLower();
        return !allText.Contains("paid") && 
               !allText.Contains("settled") &&
               !allText.Contains("closed");
    }

    private async Task<BaseResponse<string>> SubmitFormAsync(string url, FormUrlEncodedContent formContent)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            // Enhanced headers to better mimic a real browser
            request.Headers.Clear();
            request.Headers.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            // Remove Accept-Encoding to avoid compression issues
            // request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Cache-Control", "max-age=0");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", "same-origin");
            request.Headers.Add("Sec-Fetch-User", "?1");
            request.Headers.Add("Sec-Ch-Ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
            request.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
            request.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            
            var baseUri = new Uri(url);
            request.Headers.Add("Origin", $"{baseUri.Scheme}://{baseUri.Host}");
            request.Headers.Add("Referer", url);
            
            request.Content = formContent;
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            Logger.LogDebug("Form submission response status: {StatusCode}", response.StatusCode);
            Logger.LogDebug("Form submission response content: {Content}", content.Length > 500 ? content.Substring(0, 500) + "..." : content);
            
            // Check if response contains JSON error about token
            if (content.Contains("\"success\":false", StringComparison.OrdinalIgnoreCase) && 
                content.Contains("Token not provided", StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogInformation("Server returned JSON error about missing token, treating as no results");
                return BaseResponse<string>.Failure(400, "Token not provided");
            }
            
            if (response.IsSuccessStatusCode)
            {
                return BaseResponse<string>.Success(content);
            }
            
            var errorMessage = $"HTTP {(int)response.StatusCode}: Form submission failed";
            Logger.LogWarning("Form submission failed with status {StatusCode}", response.StatusCode);
            
            return BaseResponse<string>.Failure((int)response.StatusCode, errorMessage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during form submission");
            return BaseResponse<string>.Failure(-1, $"Form submission exception: {ex.Message}");
        }
    }

    private async Task<BaseResponse<string>> GetPageAsync(string url)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            // Enhanced headers to better mimic a real browser
            request.Headers.Clear();
            request.Headers.Add("User-Agent", 
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", 
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            // Remove Accept-Encoding to avoid compression issues
            // request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Cache-Control", "no-cache");
            request.Headers.Add("Pragma", "no-cache");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-Site", "none");
            request.Headers.Add("Sec-Fetch-User", "?1");
            request.Headers.Add("Sec-Ch-Ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
            request.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
            request.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
            
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            
            Logger.LogDebug("GET request response status: {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                return BaseResponse<string>.Success(content);
            }
            
            var errorMessage = $"HTTP {(int)response.StatusCode}: GET request failed";
            Logger.LogWarning("GET request failed with status {StatusCode}", response.StatusCode);
            
            return BaseResponse<string>.Failure((int)response.StatusCode, errorMessage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Exception during GET request");
            return BaseResponse<string>.Failure(-1, $"GET request exception: {ex.Message}");
        }
    }

    private static string CleanHtmlText(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;
        
        // Remove HTML tags
        var text = Regex.Replace(html, @"<[^>]+>", "");
        
        // Decode HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);
        
        // Clean up whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();
        
        return text;
    }
    
    private static decimal ParseAmount(string amountText)
    {
        if (string.IsNullOrEmpty(amountText))
        {
            return 0;
        }
        
        var numericText = Regex.Replace(amountText, @"[^\d\.]", "");
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
    
    private List<KeyValuePair<string, string>> CreateFormData(string licensePlate)
    {
        return
        [
            new KeyValuePair<string, string>(GetLicensePlateFieldName(), licensePlate),
            new KeyValuePair<string, string>(GetStateFieldName(), "FL_89"),
        ];
    }
    
    private string ExtractFormActionUrl(string html, string baseUrl)
    {
        try
        {
            Logger.LogDebug("Extracting form action URL from HTML content");
            
            // Try multiple form patterns to find the search form
            var formPatterns = new[]
            {
                // Standard form with action attribute
                @"<form[^>]*action=['""]([^'""]*)['""][^>]*>",
                @"<form[^>]*action=([^'""\s>]+)[^>]*>",
                
                // Form with method POST (likely search form)
                @"<form[^>]*method=['""]post['""][^>]*action=['""]([^'""]*)['""][^>]*>",
                @"<form[^>]*action=['""]([^'""]*)['""][^>]*method=['""]post['""][^>]*>",
                
                // Look for forms containing license plate or search related fields
                @"<form[^>]*>[\s\S]*?(?:plate|search|citation|violation)[\s\S]*?</form>",
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
                        Logger.LogDebug("Found form action URL: {ActionUrl}", foundActionUrl);
                        break;
                    }
                    
                    // For the last pattern (form content search), extract action from the full form
                    if (pattern.Contains("plate|search"))
                    {
                        var formContent = match.Value;
                        var actionMatch = Regex.Match(formContent, @"action=['""]([^'""]*)['""]", RegexOptions.IgnoreCase);
                        if (actionMatch.Success)
                        {
                            foundActionUrl = actionMatch.Groups[1].Value;
                            Logger.LogDebug("Found form action URL in search form: {ActionUrl}", foundActionUrl);
                            break;
                        }
                    }
                }
                
                if (!string.IsNullOrEmpty(foundActionUrl))
                    break;
            }
            
            // If still no action found, look for forms without explicit action (defaults to current page)
            if (string.IsNullOrEmpty(foundActionUrl))
            {
                var formWithoutAction = Regex.Match(html, @"<form[^>]*>", RegexOptions.IgnoreCase);
                if (formWithoutAction.Success && !formWithoutAction.Value.Contains("action=", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogDebug("Found form without action attribute, using current page URL");
                    foundActionUrl = ""; // Empty action means current page
                }
            }
            
            if (!string.IsNullOrEmpty(foundActionUrl))
            {
                // Process the found action URL
                if (string.IsNullOrEmpty(foundActionUrl) || foundActionUrl == "#")
                {
                    // Empty or # action means submit to current page
                    Logger.LogDebug("Form action is empty or #, using base URL");
                    return baseUrl;
                }
                
                // If it's a relative URL starting with /, make it absolute
                if (foundActionUrl.StartsWith("/"))
                {
                    var baseUri = new Uri(baseUrl);
                    var absoluteUrl = $"{baseUri.Scheme}://{baseUri.Host}{foundActionUrl}";
                    Logger.LogDebug("Converted relative URL {RelativeUrl} to absolute {AbsoluteUrl}", foundActionUrl, absoluteUrl);
                    return absoluteUrl;
                }
                
                // If it's already absolute, return as-is
                if (foundActionUrl.StartsWith("http"))
                {
                    Logger.LogDebug("Using absolute URL: {AbsoluteUrl}", foundActionUrl);
                    return foundActionUrl;
                }
                
                // If it's a relative path without leading slash
                var baseUriForRelative = new Uri(baseUrl);
                var resolvedUrl = new Uri(baseUriForRelative, foundActionUrl).ToString();
                Logger.LogDebug("Resolved relative URL {RelativeUrl} to {ResolvedUrl}", foundActionUrl, resolvedUrl);
                return resolvedUrl;
            }
            
            // If no form action found at all, fall back to base URL
            Logger.LogWarning("No form action found in HTML, using base URL for form submission");
            return baseUrl;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error extracting form action URL, using base URL");
            return baseUrl;
        }
    }
    
    private void ExtractAndAddTokens(string html, List<KeyValuePair<string, string>> formData)
    {
        try
        {
            // Common token patterns to look for
            var tokenPatterns = new[]
            {
                // CSRF tokens
                (@"<input[^>]*name=['""]__RequestVerificationToken['""][^>]*value=['""]([^'""]*)['""]", "__RequestVerificationToken"),
                (@"<input[^>]*name=['""]_token['""][^>]*value=['""]([^'""]*)['""]", "_token"),
                (@"<input[^>]*name=['""]csrf_token['""][^>]*value=['""]([^'""]*)['""]", "csrf_token"),
                (@"<input[^>]*name=['""]authenticity_token['""][^>]*value=['""]([^'""]*)['""]", "authenticity_token"),
                
                // Generic hidden tokens
                (@"<input[^>]*type=['""]hidden['""][^>]*name=['""]token['""][^>]*value=['""]([^'""]*)['""]", "token"),
                (@"<input[^>]*name=['""]token['""][^>]*type=['""]hidden['""][^>]*value=['""]([^'""]*)['""]", "token"),
                
                // API tokens
                (@"<input[^>]*name=['""]api_token['""][^>]*value=['""]([^'""]*)['""]", "api_token"),
                (@"<input[^>]*name=['""]access_token['""][^>]*value=['""]([^'""]*)['""]", "access_token"),
                
                // Session tokens
                (@"<input[^>]*name=['""]session_token['""][^>]*value=['""]([^'""]*)['""]", "session_token"),
                (@"<input[^>]*name=['""]form_token['""][^>]*value=['""]([^'""]*)['""]", "form_token"),
                
                // Meta tokens (sometimes in meta tags)
                (@"<meta[^>]*name=['""]csrf-token['""][^>]*content=['""]([^'""]*)['""]", "csrf-token"),
                (@"<meta[^>]*name=['""]_token['""][^>]*content=['""]([^'""]*)['""]", "_token")
            };
            
            var tokensFound = 0;
            
            foreach (var (pattern, tokenName) in tokenPatterns)
            {
                var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var tokenValue = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(tokenValue))
                    {
                        formData.Add(new KeyValuePair<string, string>(GetTokenFieldName(), tokenValue));
                        Logger.LogDebug("Found token: {TokenName} = {TokenValue}", tokenName, tokenValue.Substring(0, Math.Min(10, tokenValue.Length)) + "...");
                        tokensFound++;
                    }
                }
            }
            
            const string hiddenInputPattern = """<input[^>]*type=['"]hidden['"][^>]*name=['"]([^'"]*)['"][^>]*value=['"]([^'"]*)['"]""";
            var hiddenMatches = Regex.Matches(html, hiddenInputPattern, RegexOptions.IgnoreCase);
            
            foreach (Match match in hiddenMatches)
            {
                var fieldName = match.Groups[1].Value;
                var fieldValue = match.Groups[2].Value;
                
                // Skip if we already added this field
                if (formData.Any(kvp => kvp.Key.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                
                // Add common hidden fields that are often required
                if (fieldName.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Contains("csrf", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Contains("session", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Contains("form", StringComparison.OrdinalIgnoreCase) ||
                    fieldName.Contains("nonce", StringComparison.OrdinalIgnoreCase))
                {
                    formData.Add(new KeyValuePair<string, string>(GetTokenFieldName(), fieldValue));
                    Logger.LogDebug("Found additional hidden field: {FieldName} = {FieldValue}", fieldName, fieldValue.Substring(0, Math.Min(10, fieldValue.Length)) + "...");
                    tokensFound++;
                }
            }
            
            if (tokensFound == 0)
            {
                Logger.LogWarning("No tokens found in HTML - this might cause 'Token not provided' errors");
            }
            else
            {
                Logger.LogInformation("Found {TokenCount} tokens/hidden fields for form submission", tokensFound);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error extracting tokens from HTML");
        }
    }
    
    private string? ExtractRedirectLocation(string jsonResponse, string baseUrl)
    {
        try
        {
            Logger.LogDebug("Extracting redirect location from JSON response");
            
            const string locationPattern = """
                                           "location"\s*:\s*"([^"]*)"|'location'\s*:\s*'([^']*)'
                                           """;
            var match = Regex.Match(jsonResponse, locationPattern, RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var location = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                location = location.Replace("\\/", "/");
                
                Logger.LogDebug("Extracted location from JSON: {Location}", location);
                
                if (location.StartsWith("/"))
                {
                    var baseUri = new Uri(baseUrl);
                    var absoluteUrl = $"{baseUri.Scheme}://{baseUri.Host}{location}";
                    Logger.LogDebug("Converted relative location {RelativeLocation} to absolute {AbsoluteUrl}", location, absoluteUrl);
                    return absoluteUrl;
                }
                
                if (location.StartsWith("http"))
                {
                    Logger.LogDebug("Using absolute location: {AbsoluteUrl}", location);
                    return location;
                }
                
                var baseUriForRelative = new Uri(baseUrl);
                var resolvedUrl = new Uri(baseUriForRelative, location).ToString();
                Logger.LogDebug("Resolved relative location {RelativeLocation} to {ResolvedUrl}", location, resolvedUrl);
                return resolvedUrl;
            }
            
            Logger.LogWarning("Could not extract location from JSON response: {JsonResponse}", jsonResponse);
            return null;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error extracting redirect location from JSON");
            return null;
        }
    }
}
