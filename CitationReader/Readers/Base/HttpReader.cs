using System.Net;

namespace CitationReader.Readers.Base;

public abstract class HttpReader
{
    public HttpReader()
    {
        HttpClient = ProduceHttpClient();
    }
    
    protected HttpClient HttpClient { get; }

    private static HttpClient ProduceHttpClient()
    {
        var cookieJar = new CookieContainer();
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = cookieJar,
            AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
        };
        
        var httpClient = new HttpClient(handler);

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36"
        );
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        httpClient.DefaultRequestHeaders.Referrer = new Uri("https://site.metropolis.io/");
        httpClient.DefaultRequestHeaders.Add("Origin", "https://site.metropolis.io");

        return httpClient;
    }
}