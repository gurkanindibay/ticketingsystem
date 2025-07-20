using TicketingSystem.Shared.DTOs;
using TicketingSystem.Shared.Models;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Interface for ticket-related business operations
    /// </summary>
    public interface ITicketService
    {
        /// <summary>
        /// Purchase tickets for an event with payment processing
        /// </summary>
        /// <param name="request">Ticket purchase request</param>
        /// <param name="userId">Current user ID</param>
        /// <returns>Purchase result with ticket details</returns>
        Task<PurchaseTicketResponse> PurchaseTicketsAsync(PurchaseTicketRequest request, string userId);

        /// <summary>
        /// Cancel a ticket purchase and process refund
        /// </summary>
        /// <param name="transactionId">Transaction ID to cancel</param>
        /// <param name="userId">Current user ID</param>
        /// <returns>Cancellation result</returns>
        Task<bool> CancelTicketAsync(string transactionId, string userId);

        /// <summary>
        /// Get user's purchased tickets with pagination
        /// </summary>
        /// <param name="request">Request with pagination options</param>
        /// <param name="userId">Current user ID</param>
        /// <returns>User's tickets</returns>
        Task<UserTicketsResponse> GetUserTicketsAsync(UserTicketsRequest request, string userId);

        /// <summary>
        /// Get ticket details by transaction ID
        /// </summary>
        /// <param name="transactionId">Transaction ID</param>
        /// <param name="userId">Current user ID</param>
        /// <returns>Ticket details</returns>
        Task<PurchaseTicketResponse?> GetTicketByTransactionAsync(string transactionId, string userId);

        /// <summary>
        /// Check event availability and get pricing
        /// </summary>
        /// <param name="eventId">Event ID</param>
        /// <param name="quantity">Number of tickets requested</param>
        /// <returns>Availability and pricing information</returns>
        Task<(bool isAvailable, decimal pricePerTicket, int availableTickets)> CheckEventAvailabilityAsync(int eventId, int quantity);
    }
}
