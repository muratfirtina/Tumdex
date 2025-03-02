using Domain;
using Domain.Entities;
using Domain.Identity;

namespace Application.Services;

public interface INewsletterService
{
    /// <summary>
    /// Subscribes an email to the newsletter
    /// </summary>
    Task<Newsletter> SubscribeAsync(string email, string? source = null, string? userId = null);
    
    /// <summary>
    /// Unsubscribes using a token from the unsubscribe link
    /// </summary>
    Task<Newsletter> UnsubscribeAsync(string token);
    
    /// <summary>
    /// Unsubscribes directly using an email address
    /// </summary>
    Task<Newsletter> UnsubscribeAsync(string email, bool isDirectCall = false);
    
    /// <summary>
    /// Handles subscription when a user registers
    /// </summary>
    Task HandleUserRegistrationAsync(AppUser user);
    
    /// <summary>
    /// Sends the monthly newsletter to all active subscribers
    /// </summary>
    /// <param name="isTest">If true, sends only to a small test group</param>
    Task SendMonthlyNewsletterAsync(bool isTest = false);
    
    /// <summary>
    /// Queues the newsletter to be sent in the background
    /// </summary>
    /// <param name="isTest">If true, sends only to a small test group</param>
    Task QueueSendMonthlyNewsletterAsync(bool isTest = false);
}