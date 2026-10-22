namespace STEX.Inv.Desktop.Infrastructure.Http;

using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using STEX.Inv.Desktop.Core.Interfaces;

public class JwtAuthenticationHandler : DelegatingHandler
{
    private readonly ISecureStorageService _secureStorageService;

    public JwtAuthenticationHandler(ISecureStorageService secureStorageService)
    {
        _secureStorageService = secureStorageService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _secureStorageService.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}