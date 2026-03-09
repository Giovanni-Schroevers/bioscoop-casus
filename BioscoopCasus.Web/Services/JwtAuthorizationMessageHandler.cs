using System.Net.Http.Headers;
using Microsoft.JSInterop;

namespace BioscoopCasus.Web.Services;

public class JwtAuthorizationMessageHandler(IJSRuntime jsRuntime) : DelegatingHandler
{
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private const string AuthTokenKey = "authToken";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _jsRuntime.InvokeAsync<string>("localStorageInterop.getItem", AuthTokenKey);

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
