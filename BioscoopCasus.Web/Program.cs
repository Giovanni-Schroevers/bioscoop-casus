using System;
using System.Net.Http;
using BioscoopCasus.Models.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BioscoopCasus.Web;
using BioscoopCasus.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register authentication & authorization components
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<MoviesOverviewService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddHttpClient<SeatSelectionService>(client => 
    client.BaseAddress = new Uri("http://localhost:5064/"));
builder.Services.AddScoped<MoviesOverviewService>();
builder.Services.AddScoped<MovieInformationService>();
builder.Services.AddScoped<TicketPricingService>();
builder.Services.AddSingleton<QrCodeHelper>();

// Register the JWT handler
builder.Services.AddTransient<JwtAuthorizationMessageHandler>();

// Register services as Typed Clients with the JWT handler
builder.Services.AddHttpClient<MovieService>(client =>
    client.BaseAddress = new Uri("http://localhost:5064/"))
    .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

builder.Services.AddHttpClient<RoomService>(client =>
    client.BaseAddress = new Uri("http://localhost:5064/"))
    .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

builder.Services.AddHttpClient<ShowtimeService>(client =>
    client.BaseAddress = new Uri("http://localhost:5064/"))
    .AddHttpMessageHandler<JwtAuthorizationMessageHandler>();

// Default HttpClient for services that don't use the JWT handler
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5064/") });

await builder.Build().RunAsync();
