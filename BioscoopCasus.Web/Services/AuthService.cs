using System.Net.Http.Json;
using BioscoopCasus.Models.DTOs;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace BioscoopCasus.Web.Services;

public class AuthService(HttpClient httpClient, AuthenticationStateProvider authenticationStateProvider, IJSRuntime jsRuntime)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly AuthenticationStateProvider _authenticationStateProvider = authenticationStateProvider;
    private readonly IJSRuntime _jsRuntime = jsRuntime;
    private const string AuthTokenKey = "authToken";

    public async Task<string?> LoginAsync(LoginDto loginRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);

        if (!response.IsSuccessStatusCode)
        {
            return "Invalid login credentials";
        }

        var authResult = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

        if (authResult?.Token != null)
        {
            await _jsRuntime.InvokeVoidAsync("localStorageInterop.setItem", AuthTokenKey, authResult.Token);
            ((CustomAuthenticationStateProvider)_authenticationStateProvider).NotifyUserAuthentication(authResult.Token);
            return null; // Success (no error string)
        }

        return "Invalid response from server";
    }

    public async Task LogoutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorageInterop.removeItem", AuthTokenKey);
        ((CustomAuthenticationStateProvider)_authenticationStateProvider).NotifyUserLogout();
    }
}
