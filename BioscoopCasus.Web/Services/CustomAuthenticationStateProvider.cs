using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace BioscoopCasus.Web.Services;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IJSRuntime _jsRuntime;
    private const string AuthTokenKey = "authToken";

    public CustomAuthenticationStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await _jsRuntime.InvokeAsync<string>("localStorageInterop.getItem", AuthTokenKey);

        if (string.IsNullOrWhiteSpace(token))
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        return new AuthenticationState(user);
    }

    public void NotifyUserAuthentication(string token)
    {
        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);
        var authState = Task.FromResult(new AuthenticationState(user));
        NotifyAuthenticationStateChanged(authState);
    }

    public void NotifyUserLogout()
    {
        var authState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
        NotifyAuthenticationStateChanged(authState);
    }

    private static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
        return keyValuePairs?.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()!)) ?? Enumerable.Empty<Claim>();
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
