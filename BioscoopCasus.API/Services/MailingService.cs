using System.Net;
using System.Net.Mail;
using BioscoopCasus.API.Data;
using BioscoopCasus.API.Entities;
using BioscoopCasus.Models.DTOs;
using BioscoopCasus.Models.Helpers;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Services;

public class MailingService(BioscoopDbContext context, IConfiguration configuration)
{
    private const string Host = "smtp.mailgun.org";
    private readonly string _username = configuration["Mailgun:Username"] ?? string.Empty;
    private readonly string _password = configuration["Mailgun:Password"] ?? string.Empty;
    private readonly string _webAppBaseUrl = configuration["WebApp:BaseUrl"] ?? string.Empty;
    private readonly QrCodeHelper _qrCodeHelper = new();

    public async Task SendReservationEmailAsync(string recipientEmail, ReservationResponseDto reservation)
    {
        var qrCodeText = _qrCodeHelper.GetQrCodeString(reservation);
        var qrCodeImageBase64 = _qrCodeHelper.GetQrCodeImage(qrCodeText);
        var qrCodeBytes = Convert.FromBase64String(qrCodeImageBase64);
        var ticketPrintUrl = $"{_webAppBaseUrl.TrimEnd('/')}/TicketPrint/{reservation.Id}";
        const string qrCodeContentId = "qrcode";

        var htmlContent = await BuildReservationEmailContentAsync(reservation, qrCodeContentId, ticketPrintUrl);

        using var message = new MailMessage();
        message.From = new MailAddress(_username, "Cinema");
        message.Subject = $"Ticket: {reservation.MovieTitle}";
        message.Body = htmlContent;
        message.IsBodyHtml = true;

        message.Headers.Add("X-Entity-Ref-ID", $"reservation-{Guid.NewGuid()}");
        message.To.Add(new MailAddress(recipientEmail));

        var qrCodeAttachment = new Attachment(new MemoryStream(qrCodeBytes), "qrcode.png", "image/png")
        {
            ContentId = qrCodeContentId,
            ContentDisposition = { Inline = true, DispositionType = "inline" }
        };
        message.Attachments.Add(qrCodeAttachment);

        using var smtpClient = CreateSmtpClient();
        await smtpClient.SendMailAsync(message);
    }

    public async Task SendNewsletterConfirmationEmailAsync(NewsletterSubscriber subscriber)
    {
        var template = await context.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(emailTemplate => emailTemplate.Name == "NewsletterConfirmation");

        if (template is null || string.IsNullOrWhiteSpace(template.Body))
            throw new InvalidOperationException("Email template 'NewsletterConfirmation' not found.");

        var values = new Dictionary<string, string>
        {
            ["Name"] = subscriber.Name ?? "klant",
            ["Email"] = subscriber.Email
        };

        var htmlContent = BuildHtmlDocument(MailHelper.Render(template.Body, values));
        var subject = string.IsNullOrWhiteSpace(template.Subject)
            ? "Newsletter subscription confirmation"
            : MailHelper.Render(template.Subject, values);

        await SendHtmlEmailAsync(subscriber.Email, subject, htmlContent, $"newsletter-confirmation-{Guid.NewGuid()}");
    }

    public async Task SendPendingNewsletterEmailsAsync(CancellationToken cancellationToken = default)
    {
        var subscribers = await context.NewsletterSubscribers
            .Where(subscriber => subscriber.ConfirmationSent)
            .ToListAsync(cancellationToken);

        if (subscribers.Count == 0)
            return;

        var newsletterTemplates = await context.EmailTemplates
            .AsNoTracking()
            .Where(emailTemplate => emailTemplate.Name.Contains("NewsletterEmail"))
            .ToListAsync(cancellationToken);

        if (newsletterTemplates.Count == 0)
            return;

        foreach (var subscriber in subscribers)
        {
            var lastReceivedMoment = subscriber.LatestReceivedEmail;

            var templatesToSend = newsletterTemplates
                .Where(template =>
                {
                    var templateMoment = template.ChangedOn ?? template.CreatedAt;
                    return !lastReceivedMoment.HasValue || templateMoment > lastReceivedMoment;
                })
                .OrderBy(template => template.ChangedOn ?? template.CreatedAt)
                .ToList();

            if (templatesToSend.Count == 0)
                continue;

            foreach (var template in templatesToSend)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var values = new Dictionary<string, string>
                {
                    ["Name"] = subscriber.Name ?? "klant",
                    ["Email"] = subscriber.Email
                };

                var subject = string.IsNullOrWhiteSpace(template.Subject)
                    ? "Cinema newsletter"
                    : MailHelper.Render(template.Subject, values);

                var htmlContent = BuildHtmlDocument(MailHelper.Render(template.Body, values));

                await SendHtmlEmailAsync(
                    subscriber.Email,
                    subject,
                    htmlContent,
                    $"newsletter-{template.Id}-{subscriber.Id}");
            }

            subscriber.LatestReceivedEmail = DateTime.Now;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> BuildReservationEmailContentAsync(
        ReservationResponseDto reservation,
        string qrCodeContentId,
        string ticketPrintUrl)
    {
        var seatsByRow = reservation.Seats
            .GroupBy(seat => seat.Row)
            .OrderBy(group => group.Key)
            .Select(group =>
                $"Row {group.Key}: {string.Join(", ", group.OrderBy(seat => seat.SeatNumber).Select(seat => $"Seat {seat.SeatNumber}"))}")
            .ToList();

        var seatsList = string.Join("<br>", seatsByRow);
        var showtimeFormatted = reservation.Showtime.StartTime.ToString("dddd, dd MMMM, yyyy 'at' HH:mm");

        var template = await context.EmailTemplates
            .Where(emailTemplate => emailTemplate.Name == "ReservationTemplate")
            .Select(emailTemplate => emailTemplate.Body)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(template))
            throw new InvalidOperationException("Email template 'ReservationTemplate' not found.");

        var values = new Dictionary<string, string>
        {
            ["ReservationId"] = reservation.Id.ToString(),
            ["MovieTitle"] = reservation.MovieTitle,
            ["RoomName"] = reservation.RoomName,
            ["Showtime"] = showtimeFormatted,
            ["Seats"] = seatsList,
            ["QrCodeContentId"] = qrCodeContentId,
            ["TicketPrintUrl"] = ticketPrintUrl
        };

        var renderedBody = MailHelper.Render(template, values);
        return BuildHtmlDocument(renderedBody, reservation.MovieTitle);
    }

    private async Task SendHtmlEmailAsync(string recipientEmail, string subject, string htmlContent, string entityReferenceId)
    {
        using var message = new MailMessage();
        message.From = new MailAddress(_username, "Cinema");
        message.Subject = subject;
        message.Body = htmlContent;
        message.IsBodyHtml = true;

        message.Headers.Add("X-Entity-Ref-ID", entityReferenceId);
        message.To.Add(new MailAddress(recipientEmail));

        using var smtpClient = CreateSmtpClient();
        await smtpClient.SendMailAsync(message);
    }

    private SmtpClient CreateSmtpClient()
    {
        return new SmtpClient(Host)
        {
            Port = 587,
            Credentials = new NetworkCredential(_username, _password),
            EnableSsl = true
        };
    }

    private static string BuildHtmlDocument(string bodyContent, string title = "Cinema")
    {
        return $"""
                <!DOCTYPE html>
                <html lang="en">
                    <head>
                        <meta charset="UTF-8">
                        <meta name="viewport" content="width=device-width, initial-scale=1.0">
                        <title>{title}</title>
                    </head>
                    <body>
                        {bodyContent}
                    </body>
                </html>
                """;
    }
}