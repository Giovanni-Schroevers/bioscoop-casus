using System.Net;
using System.Net.Mail;
using BioscoopCasus.Models.DTOs;
using BioscoopCasus.Models.Helpers;

namespace BioscoopCasus.API.Services;

public class MailingService(IConfiguration configuration)
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
        var htmlContent = BuildEmailContent(reservation, qrCodeContentId, ticketPrintUrl);

        using var message = new MailMessage
        {
            From = new MailAddress(_username, "Cinema"),
            Subject = $"Your ticket for {reservation.MovieTitle}",
            Body = htmlContent,
            IsBodyHtml = true
        };

        // Prevent threading of emails
        message.Headers.Add("X-Entity-Ref-ID", $"reservation-{Guid.NewGuid()}");
        message.To.Add(new MailAddress(recipientEmail));

        var qrCodeAttachment = new Attachment(new MemoryStream(qrCodeBytes), "qrcode.png", "image/png")
        {
            ContentId = qrCodeContentId,
            ContentDisposition = { Inline = true, DispositionType = "inline" }
        };
        message.Attachments.Add(qrCodeAttachment);

        using var smtpClient = new SmtpClient(Host)
        {
            Port = 587,
            Credentials = new NetworkCredential(_username, _password),
            EnableSsl = true
        };

        await smtpClient.SendMailAsync(message);
    }

    private string BuildEmailContent(ReservationResponseDto reservation, string qrCodeContentId, string ticketPrintUrl)
    {
        var seatsByRow = reservation.Seats
            .GroupBy(s => s.Row)
            .OrderBy(g => g.Key)
            .Select(g => $"Row {g.Key}: {string.Join(", ", g.OrderBy(s => s.SeatNumber).Select(s => $"Seat {s.SeatNumber}"))}")
            .ToList();
        var seatsList = string.Join("<br>", seatsByRow);
        var showtimeFormatted = reservation.Showtime.StartTime.ToString("dddd, dd MMMM, yyyy 'at' HH:mm");

        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Movie Ticket</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 8px;
            padding: 30px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            border-bottom: 2px solid #e74c3c;
            padding-bottom: 20px;
            margin-bottom: 30px;
        }}
        .header h1 {{
            color: #e74c3c;
            margin: 0;
            font-size: 28px;
        }}
        .info-section {{
            margin-bottom: 25px;
        }}
        .info-section h2 {{
            color: #2c3e50;
            font-size: 18px;
            margin-bottom: 10px;
            border-left: 4px solid #e74c3c;
            padding-left: 10px;
        }}
        .info-row {{
            margin-bottom: 12px;
            padding: 8px 0;
        }}
        .info-label {{
            font-weight: bold;
            color: #555;
            display: inline-block;
            min-width: 100px;
        }}
        .info-value {{
            color: #333;
        }}
        .qr-section {{
            text-align: center;
            margin: 30px 0;
            padding: 20px;
            background-color: #f8f9fa;
            border-radius: 8px;
        }}
        .qr-code {{
            max-width: 250px;
            height: auto;
            margin: 15px 0;
        }}
        .ticket-code {{
            font-family: 'Courier New', monospace;
            font-size: 14px;
            color: #666;
            margin-top: 10px;
            word-break: break-all;
        }}
        .button-container {{
            text-align: center;
            margin: 30px 0;
        }}
        .button {{
            display: inline-block;
            padding: 12px 30px;
            background-color: #e74c3c;
            color: #ffffff !important;
            text-decoration: none;
            border-radius: 5px;
            font-weight: bold;
            transition: background-color 0.3s;
        }}
        .button:visited {{
            color: #ffffff;
        }}
        .button:hover {{
            background-color: #c0392b;
            color: #ffffff;
        }}
        .footer {{
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #ddd;
            text-align: center;
            color: #777;
            font-size: 12px;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎬 Movie Ticket</h1>
        </div>

        <div class=""info-section"">
            <h2>Movie Information</h2>
            <div class=""info-row"">
                <span class=""info-label"">Movie:</span>
                <span class=""info-value"">{EscapeHtml(reservation.MovieTitle)}</span>
            </div>
            <div class=""info-row"">
                <span class=""info-label"">Room:</span>
                <span class=""info-value"">{EscapeHtml(reservation.RoomName)}</span>
            </div>
            <div class=""info-row"">
                <span class=""info-label"">Time:</span>
                <span class=""info-value"">{showtimeFormatted}</span>
            </div>
            <div class=""info-row"">
                <span class=""info-label"">Seats:</span>
                <span class=""info-value"">{seatsList}</span>
            </div>
        </div>

        <div class=""qr-section"">
            <h2 style=""margin-top: 0;"">Your QR Code</h2>
            <img src=""cid:{qrCodeContentId}"" alt=""QR Code"" class=""qr-code"" />
            <div class=""ticket-code"">Reservation number: {reservation.Id}</div>
        </div>

        <div class=""button-container"">
            <a href=""{EscapeHtml(ticketPrintUrl)}"" class=""button"">View & Print Ticket</a>
        </div>

        <div class=""footer"">
            <p>Thank you for your reservation!</p>
            <p>Bring this QR code to the cinema for access.</p>
        </div>
    </div>
</body>
</html>";
    }

    private static string EscapeHtml(string input) 
        => string.IsNullOrEmpty(input) ? string.Empty : WebUtility.HtmlEncode(input);
}