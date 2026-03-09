using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BioscoopCasus.Web;
using BioscoopCasus.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register authentication & authorization components
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthService>();

// Register the JWT handler
builder.Services.AddTransient<JwtAuthorizationMessageHandler>();

// Register services as Typed Clients with the JWT handler
builder.Services.AddHttpClient<MovieService>(client => 
    client.BaseAddress = new Uri("https://localhost:7181/"))
    .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

builder.Services.AddHttpClient<RoomService>(client => 
    client.BaseAddress = new Uri("https://localhost:7181/"))
    .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

builder.Services.AddHttpClient<ShowtimeService>(client => 
    client.BaseAddress = new Uri("https://localhost:7181/"))
    .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

// Default HttpClient for AuthService (doesn't need the JWT handler for login)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https://localhost:7181/") });

await builder.Build().RunAsync();