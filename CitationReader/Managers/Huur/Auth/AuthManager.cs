using CitationReader.Enums;
using CitationReader.Managers.Base;
using CitationReader.Models;
using CitationReader.Models.Base;
using CitationReader.Models.Huur.Requests;

namespace CitationReader.Managers.Huur.Auth
{
    public class AuthManager : BaseHttpManager, IAuthManager
    {
        public AuthManager() 
            : base(HttpClientType.Auth)
        {
        }

        public Task<BaseResponse<AuthDto>> AuthorizeAsync(SignInRequest request)
        {
            Logger.LogInformation("Starting sign-in process for user: {Email}", request.Email);
            
            return RequestAsync<AuthDto>(
                HttpMethod.Post,
                "UserAuth/signin",
                request);
        }
    }
}
