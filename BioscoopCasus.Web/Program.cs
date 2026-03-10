using System;
using System.Net.Http;
using BioscoopCasus.Models.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BioscoopCasus.Web;
using BioscoopCasus.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = new Uri("https://localhost:7181/");

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

var host = builder.Build();

// Initialize pricing configuration BEFORE the app runs
var pricingService = host.Services.GetRequiredService<TicketPricingService>();
var httpClient = host.Services.GetRequiredService<HttpClient>();

await pricingService.InitializeAsync(httpClient);

await host.RunAsync();