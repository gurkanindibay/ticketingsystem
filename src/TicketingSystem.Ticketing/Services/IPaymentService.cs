using TicketingSystem.Shared.DTOs;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Interface for payment processing services
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// Process a payment for ticket purchase
        /// </summary>
        /// <param name="request">Payment request details</param>
        /// <returns>Payment processing result</returns>
        Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request);

        /// <summary>
        /// Process a refund for a previous payment
        /// </summary>
        /// <param name="request">Refund request details</param>
        /// <returns>Refund processing result</returns>
        Task<RefundResponse> ProcessRefundAsync(RefundRequest request);

        /// <summary>
        /// Get payment status by payment ID
        /// </summary>
        /// <param name="paymentId">Payment ID to check</param>
        /// <returns>Current payment status</returns>
        Task<PaymentResponse?> GetPaymentStatusAsync(string paymentId);

        /// <summary>
        /// Validate payment request before processing
        /// </summary>
        /// <param name="request">Payment request to validate</param>
        /// <returns>Validation result</returns>
        bool ValidatePaymentRequest(PaymentRequest request);
    }
}
