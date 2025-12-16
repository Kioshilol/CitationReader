using CitationReader.Configuration;
using CitationReader.Enums;
using CitationReader.Managers.Base;
using CitationReader.Models;
using CitationReader.Models.Base;
using CitationReader.Models.Huur.Requests;
using Microsoft.Extensions.Options;

namespace CitationReader.Managers.Huur.Auth
{
    public class AuthManager : BaseHttpManager, IAuthManager
    {
        public AuthManager(
            IHttpClientFactory httpClientFactory,
            IOptions<HuurOptions> options, 
            ILogger<AuthManager> logger) 
            : base(HttpClientType.Auth, httpClientFactory, options, logger)
        {
        }

        public Task<BaseResponse<AuthDto>> SignInAsync(SignInRequest request)
        {
            Logger.LogInformation("Starting sign-in process for user: {Email}", request.Email);
            
            return RequestAsync<AuthDto>(
                HttpMethod.Post,
                "UserAuth/signin",
                request);
        }
    }
}
