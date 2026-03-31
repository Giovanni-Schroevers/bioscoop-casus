using BioscoopCasus.API.Data;
using BioscoopCasus.API.Entities;
using BioscoopCasus.API.Services;
using BioscoopCasus.Models.DTOs;
using BioscoopCasus.Models.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BioscoopCasus.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MailingController(BioscoopDbContext context, MailingService mailingService) : ControllerBase
{
    private readonly QrCodeHelper _qrCodeHelper = new();

    // POST /api/mailing/ticket
    [HttpPost("ticket")]
    public async Task<ActionResult<TicketMailResponseDto>> SendReservationMail([FromBody] TicketMailSendDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("Email is required");

        if (dto.ReservationId == 0)
            return BadRequest("Reservation ID is required");
        
        var reservationId = dto.ReservationId;

        var reservation = await context.Reservations
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(r => r.Showtime)
                .ThenInclude(s => s.Room)
            .Include(r => r.ShowtimeSeats)
                .ThenInclude(ss => ss.Seat)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation is null)
            return NotFound("Reservation not found");

        var seats = reservation.ShowtimeSeats
            .Select(ss => new SeatDto(
                ss.SeatId,
                ss.Seat.Row,
                ss.Seat.SeatNumber))
            .ToList();

        var reservationDto = new ReservationResponseDto(
            reservation.Id,
            new ShowtimeResponseDto(
                reservation.Showtime.Id,
                reservation.Showtime.MovieId,
                reservation.Showtime.RoomId,
                reservation.Showtime.StartTime,
                0
            ),
            seats,
            reservation.Showtime.Movie.Title,
            reservation.Showtime.Room.Name
        );

        try
        {
            await mailingService.SendReservationEmailAsync(dto.Email, reservationDto);
            return Ok(new TicketMailResponseDto(true));
        }
        catch (Exception)
        {
            return StatusCode(500, new TicketMailResponseDto(false));
        }
    }
    
     // GET /api/mailing/email-template
    [HttpGet("email-template")]
    public async Task<ActionResult<List<EmailTemplateDto>>> GetEmailTemplates()
    {
        try
        {
            var emailTemplates = await context.EmailTemplates
                .OrderBy(emailTemplate => emailTemplate.Name)
                .Select(emailTemplate => new EmailTemplateDto
                {
                    Id = emailTemplate.Id,
                    Name = emailTemplate.Name,
                    Subject = emailTemplate.Subject,
                    Body = emailTemplate.Body
                })
                .ToListAsync();

            return Ok(emailTemplates);
        }
        catch (Exception)
        {
            return StatusCode(500, "Something went wrong while retrieving the email templates");
        }
    }

    // POST /api/mailing/email-template
    [HttpPost("email-template")]
    public async Task<ActionResult<EmailTemplateDto>> CreateEmailTemplate([FromBody] EmailTemplateDto emailTemplateDto)
    {
        if (string.IsNullOrWhiteSpace(emailTemplateDto.Name))
            return BadRequest("Name is required");

        if (string.IsNullOrWhiteSpace(emailTemplateDto.Body))
            return BadRequest("Body is required");

        var trimmedName = emailTemplateDto.Name.Trim();

        var existingEmailTemplate = await context.EmailTemplates
            .FirstOrDefaultAsync(emailTemplate => emailTemplate.Name == trimmedName);

        if (existingEmailTemplate is not null)
            return BadRequest("An email template with this name already exists");

        try
        {
            var highestId = await context.EmailTemplates
                .MaxAsync(emailTemplate => (int?)emailTemplate.Id) ?? 0;

            var emailTemplate = new EmailTemplates
            {
                Id = highestId + 1,
                Name = trimmedName,
                Subject = emailTemplateDto.Subject,
                Body = emailTemplateDto.Body,
                CreatedAt = DateTime.Now
            };

            context.EmailTemplates.Add(emailTemplate);
            await context.SaveChangesAsync();

            return Ok(new EmailTemplateDto
            {
                Id = emailTemplate.Id,
                Name = emailTemplate.Name,
                Subject = emailTemplate.Subject,
                Body = emailTemplate.Body
            });
        }
        catch (Exception)
        {
            return StatusCode(500, "Something went wrong while saving the email template");
        }
    }

    // PUT /api/mailing/email-template/{id}
    [HttpPut("email-template/{id:int}")]
    public async Task<ActionResult<EmailTemplateDto>> UpdateEmailTemplate(int id, [FromBody] EmailTemplateDto emailTemplateDto)
    {
        if (string.IsNullOrWhiteSpace(emailTemplateDto.Name))
            return BadRequest("Name is required");

        if (string.IsNullOrWhiteSpace(emailTemplateDto.Body))
            return BadRequest("Body is required");

        var trimmedName = emailTemplateDto.Name.Trim();

        var emailTemplate = await context.EmailTemplates
            .FirstOrDefaultAsync(emailTemplateEntity => emailTemplateEntity.Id == id);

        if (emailTemplate is null)
            return NotFound("Email template not found");

        var duplicateNameExists = await context.EmailTemplates
            .AnyAsync(emailTemplateEntity =>
                emailTemplateEntity.Id != id &&
                emailTemplateEntity.Name == trimmedName);

        if (duplicateNameExists)
            return BadRequest("An email template with this name already exists");

        try
        {
            emailTemplate.Name = trimmedName;
            emailTemplate.Subject = emailTemplateDto.Subject;
            emailTemplate.Body = emailTemplateDto.Body;
            emailTemplate.ChangedOn = DateTime.Now;

            await context.SaveChangesAsync();

            return Ok(new EmailTemplateDto
            {
                Id = emailTemplate.Id,
                Name = emailTemplate.Name,
                Subject = emailTemplate.Subject,
                Body = emailTemplate.Body
            });
        }
        catch (Exception)
        {
            return StatusCode(500, "Something went wrong while updating the email template");
        }
    }
    
    // POST /api/mailing/newsletter/subscribe
    [HttpPost("newsletter/subscribe")]
    public async Task<ActionResult> SubscribeToNewsletter([FromBody] NewsletterSubscriptionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required");

        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest("Email is required");

        try
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

            var existingSubscriber = await context.NewsletterSubscribers
                .FirstOrDefaultAsync(subscriber => subscriber.Email.ToLower() == normalizedEmail);

            if (existingSubscriber is not null)
                return BadRequest("This email address is already subscribed");

            var newsletterSubscriber = new NewsletterSubscriber
            {
                Name = dto.Name.Trim(),
                Email = normalizedEmail,
                ConfirmationSent = false
            };

            await mailingService.SendNewsletterConfirmationEmailAsync(newsletterSubscriber);
            
            // If sending the email succeeded update confirmation sent and add them to the newsletter subscribers
            context.NewsletterSubscribers.Add(newsletterSubscriber);
            newsletterSubscriber.ConfirmationSent = true;
            await context.SaveChangesAsync();

            return Ok(new
            {
                Success = true,
                Message = "Newsletter subscription created and confirmation email sent"
            });
        }
        catch (Exception)
        {
            return StatusCode(500, "Something went wrong while subscribing to the newsletter");
        }
    }
}