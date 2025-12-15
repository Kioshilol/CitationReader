using System.Threading.Tasks;
using CitationReader.Models;
using CitationReader.Models.Base;
using CitationReader.Models.Huur.Requests;

namespace CitationReader.Managers.Huur.Auth
{
    public interface IAuthManager
    {
        Task<BaseResponse<AuthDto>> SignInAsync(SignInRequest request);
    }
}
