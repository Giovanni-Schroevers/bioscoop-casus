using BioscoopCasus.API.Services;

namespace BioscoopCasus.API.BackgroundServices;

public class NewsletterBackgroundService(IServiceScopeFactory serviceScopeFactory) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);

        using var periodicTimer = new PeriodicTimer(Interval);

        while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var serviceScope = serviceScopeFactory.CreateScope();
            var mailingService = serviceScope.ServiceProvider.GetRequiredService<MailingService>();

            await mailingService.SendPendingNewsletterEmailsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Console.WriteLine($"Newsletter background service failed: {exception}");
        }
    }
}