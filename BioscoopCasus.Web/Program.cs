using System;
using System.Globalization;
using System.Net.Http;
using BioscoopCasus.Models.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BioscoopCasus.Web;
using BioscoopCasus.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = new Uri("http://localhost:5064/");

// Default HttpClient used by most services
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = apiBase
});

// Auth
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthService>();

// App services
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<MoviesOverviewService>();
builder.Services.AddScoped<FilmsOverviewService>();
builder.Services.AddScoped<MovieInformationService>();
builder.Services.AddScoped<TicketPricingService>();
builder.Services.AddSingleton<QrCodeHelper>();

// JWT handler
builder.Services.AddTransient<JwtAuthorizationMessageHandler>();

// Typed clients (JWT protected)
builder.Services.AddHttpClient<MovieService>(client =>
{
    client.BaseAddress = apiBase;
}).AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

builder.Services.AddHttpClient<RoomService>(client =>
{
    client.BaseAddress = apiBase;
}).AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

builder.Services.AddHttpClient<ShowtimeService>(client =>
{
    client.BaseAddress = apiBase;
}).AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

// Seat selection uses another backend
builder.Services.AddHttpClient<SeatSelectionService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5064/");
});

builder.Services.AddHttpClient<PaymentService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5064/");
});

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var host = builder.Build();

var js = host.Services.GetRequiredService<IJSRuntime>();
var storedCulture = await js.InvokeAsync<string>("blazorCulture.get");
var culture = string.IsNullOrWhiteSpace(storedCulture) ? "en-US" : storedCulture;
var cultureInfo = new CultureInfo(culture);
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

// Initialize pricing configuration BEFORE the app runs
var pricingService = host.Services.GetRequiredService<TicketPricingService>();
var httpClient = host.Services.GetRequiredService<HttpClient>();

await pricingService.InitializeAsync(httpClient);

await host.RunAsync();
