using System.Net;
using System.Net.Mail;
using BioscoopCasus.API.Data;
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
        var htmlContent = await BuildEmailContent(reservation, qrCodeContentId, ticketPrintUrl);

        using var message = new MailMessage();

        message.From = new MailAddress(_username, "Cinema");
        message.Subject = $"Your ticket for {reservation.MovieTitle}";
        message.Body = htmlContent;
        message.IsBodyHtml = true;

        // Prevent threading of emails
        message.Headers.Add("X-Entity-Ref-ID", $"reservation-{Guid.NewGuid()}");
        message.To.Add(new MailAddress(recipientEmail));

        var qrCodeAttachment = new Attachment(new MemoryStream(qrCodeBytes), "qrcode.png", "image/png")
        {
            ContentId = qrCodeContentId,
            ContentDisposition = { Inline = true, DispositionType = "inline" }
        };
        message.Attachments.Add(qrCodeAttachment);

        using var smtpClient = new SmtpClient(Host);
        smtpClient.Port = 587;
        smtpClient.Credentials = new NetworkCredential(_username, _password);
        smtpClient.EnableSsl = true;

        await smtpClient.SendMailAsync(message);
    }

    private async Task<string> BuildEmailContent(
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

        return $"""

                <!DOCTYPE html>
                <html lang="en">
                    <head>
                        <meta charset="UTF-8">
                        <meta name="viewport" content="width=device-width, initial-scale=1.0">
                        <title>{values["MovieTitle"]}</title>
                    </head>
                    <body>
                        {renderedBody}
                    </body>
                </html>
                """;
    }
}