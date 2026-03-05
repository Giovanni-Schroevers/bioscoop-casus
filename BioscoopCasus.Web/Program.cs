using System;
using System.Net.Http;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BioscoopCasus.Web;
using BioscoopCasus.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Point the HttpClient to our backend API (running on port 5064 for HTTP to avoid SSL issues)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7181/") });

// Register authentication & authorization components
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthService>();

await builder.Build().RunAsync();