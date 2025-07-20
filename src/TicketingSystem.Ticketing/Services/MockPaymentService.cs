using System.Text.RegularExpressions;
using TicketingSystem.Shared.DTOs;
using TicketingSystem.Shared.Utilities;

namespace TicketingSystem.Ticketing.Services
{
    /// <summary>
    /// Mock payment service for simulating payment gateway operations
    /// This service simulates real payment processing behavior for development and testing
    /// </summary>
    public class MockPaymentService : IPaymentService
    {
        private readonly ILogger<MockPaymentService> _logger;
        private readonly Dictionary<string, PaymentResponse> _paymentStore;
        private readonly Dictionary<string, RefundResponse> _refundStore;
        private readonly Random _random;

        // Mock card numbers for testing different scenarios
        private readonly Dictionary<string, PaymentStatus> _testCardBehaviors = new()
        {
            { "4111111111111111", PaymentStatus.Completed }, // Successful payment
            { "4000000000000002", PaymentStatus.Failed },    // Declined card
            { "4000000000000119", PaymentStatus.Processing }, // Processing delay
            { "4000000000000127", PaymentStatus.Failed },    // Insufficient funds
            { "4242424242424242", PaymentStatus.Completed }  // Another successful card
        };

        public MockPaymentService(ILogger<MockPaymentService> logger)
        {
            _logger = logger;
            _paymentStore = new Dictionary<string, PaymentResponse>();
            _refundStore = new Dictionary<string, RefundResponse>();
            _random = new Random();
        }

        /// <summary>
        /// Process a payment for ticket purchase
        /// </summary>
        public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
        {
            try
            {
                _logger.LogInformation("Processing payment for amount: {Amount} {Currency}", 
                    request.Amount, request.Currency);

                // Simulate validation
                if (!ValidatePaymentRequest(request))
                {
                    return CreateFailedPaymentResponse(request, "VALIDATION_FAILED", "Invalid payment request");
                }

                // Simulate network delay
                await Task.Delay(_random.Next(500, 2000));

                var paymentId = $"PAY_{Guid.NewGuid():N}";
                var transactionId = SecurityHelper.GenerateTransactionId(
                    userId: 1, // This would come from authentication context
                    eventId: 0, // Not applicable for payments
                    timestamp: DateTime.UtcNow);

                // Determine payment outcome based on test card number
                var status = DeterminePaymentStatus(request.CardNumber);
                var isSuccess = status == PaymentStatus.Completed;

                var response = new PaymentResponse
                {
                    IsSuccess = isSuccess,
                    PaymentId = paymentId,
                    TransactionId = transactionId,
                    Status = status,
                    Amount = request.Amount,
                    Currency = request.Currency,
                    PaymentMethod = request.PaymentMethod,
                    ProcessedAt = DateTime.UtcNow,
                    Metadata = new Dictionary<string, string>
                    {
                        { "gateway", "mock_payment_gateway" },
                        { "card_last_four", request.CardNumber.Length >= 4 ? request.CardNumber[^4..] : "****" },
                        { "card_holder", request.CardHolderName },
                        { "processing_time_ms", _random.Next(500, 2000).ToString() }
                    }
                };

                if (!isSuccess)
                {
                    response.ErrorCode = GetErrorCodeForStatus(status);
                    response.ErrorMessage = GetErrorMessageForStatus(status);
                }

                // Store payment for later retrieval
                _paymentStore[paymentId] = response;

                _logger.LogInformation("Payment processed: {PaymentId}, Status: {Status}, Success: {IsSuccess}", 
                    paymentId, status, isSuccess);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment for amount: {Amount}", request.Amount);
                return CreateFailedPaymentResponse(request, "PROCESSING_ERROR", "Payment processing failed");
            }
        }

        /// <summary>
        /// Process a refund for a previous payment
        /// </summary>
        public async Task<RefundResponse> ProcessRefundAsync(RefundRequest request)
        {
            try
            {
                _logger.LogInformation("Processing refund for payment: {PaymentId}, Amount: {Amount}", 
                    request.PaymentId, request.Amount);

                // Check if payment exists
                if (!_paymentStore.TryGetValue(request.PaymentId, out var originalPayment))
                {
                    return new RefundResponse
                    {
                        IsSuccess = false,
                        PaymentId = request.PaymentId,
                        ErrorCode = "PAYMENT_NOT_FOUND",
                        ErrorMessage = "Original payment not found"
                    };
                }

                // Check if payment was successful
                if (originalPayment.Status != PaymentStatus.Completed)
                {
                    return new RefundResponse
                    {
                        IsSuccess = false,
                        PaymentId = request.PaymentId,
                        ErrorCode = "PAYMENT_NOT_REFUNDABLE",
                        ErrorMessage = "Only completed payments can be refunded"
                    };
                }

                // Check refund amount
                if (request.Amount > originalPayment.Amount)
                {
                    return new RefundResponse
                    {
                        IsSuccess = false,
                        PaymentId = request.PaymentId,
                        ErrorCode = "REFUND_AMOUNT_EXCEEDED",
                        ErrorMessage = "Refund amount cannot exceed original payment amount"
                    };
                }

                // Simulate processing delay
                await Task.Delay(_random.Next(300, 1000));

                var refundId = $"REF_{Guid.NewGuid():N}";
                var refundResponse = new RefundResponse
                {
                    IsSuccess = true,
                    RefundId = refundId,
                    PaymentId = request.PaymentId,
                    Amount = request.Amount,
                    Status = PaymentStatus.Refunded,
                    ProcessedAt = DateTime.UtcNow
                };

                // Store refund
                _refundStore[refundId] = refundResponse;

                _logger.LogInformation("Refund processed: {RefundId} for payment: {PaymentId}", 
                    refundId, request.PaymentId);

                return refundResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refund for payment: {PaymentId}", request.PaymentId);
                return new RefundResponse
                {
                    IsSuccess = false,
                    PaymentId = request.PaymentId,
                    ErrorCode = "REFUND_PROCESSING_ERROR",
                    ErrorMessage = "Refund processing failed"
                };
            }
        }

        /// <summary>
        /// Get payment status by payment ID
        /// </summary>
        public async Task<PaymentResponse?> GetPaymentStatusAsync(string paymentId)
        {
            await Task.Delay(100); // Simulate API call delay
            return _paymentStore.TryGetValue(paymentId, out var payment) ? payment : null;
        }

        /// <summary>
        /// Validate payment request before processing
        /// </summary>
        public bool ValidatePaymentRequest(PaymentRequest request)
        {
            try
            {
                // Validate required fields
                if (string.IsNullOrWhiteSpace(request.PaymentMethod) ||
                    string.IsNullOrWhiteSpace(request.CardNumber) ||
                    string.IsNullOrWhiteSpace(request.ExpiryMonth) ||
                    string.IsNullOrWhiteSpace(request.ExpiryYear) ||
                    string.IsNullOrWhiteSpace(request.CVV) ||
                    string.IsNullOrWhiteSpace(request.CardHolderName))
                {
                    return false;
                }

                // Validate amount
                if (request.Amount <= 0)
                {
                    return false;
                }

                // Validate card number format (basic Luhn algorithm check)
                if (!IsValidCardNumber(request.CardNumber))
                {
                    return false;
                }

                // Validate expiry date
                if (!IsValidExpiryDate(request.ExpiryMonth, request.ExpiryYear))
                {
                    return false;
                }

                // Validate CVV format
                if (!Regex.IsMatch(request.CVV, @"^\d{3,4}$"))
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating payment request");
                return false;
            }
        }

        private PaymentStatus DeterminePaymentStatus(string cardNumber)
        {
            // Use predefined test card behaviors
            if (_testCardBehaviors.TryGetValue(cardNumber, out var status))
            {
                return status;
            }

            // For other cards, randomly determine outcome with 90% success rate
            var random = _random.Next(1, 101);
            return random <= 90 ? PaymentStatus.Completed : PaymentStatus.Failed;
        }

        private static string GetErrorCodeForStatus(PaymentStatus status)
        {
            return status switch
            {
                PaymentStatus.Failed => "PAYMENT_DECLINED",
                PaymentStatus.Processing => "PAYMENT_PROCESSING",
                PaymentStatus.Cancelled => "PAYMENT_CANCELLED",
                _ => "UNKNOWN_ERROR"
            };
        }

        private static string GetErrorMessageForStatus(PaymentStatus status)
        {
            return status switch
            {
                PaymentStatus.Failed => "Payment was declined by the issuing bank",
                PaymentStatus.Processing => "Payment is still being processed",
                PaymentStatus.Cancelled => "Payment was cancelled",
                _ => "An unknown error occurred"
            };
        }

        private PaymentResponse CreateFailedPaymentResponse(PaymentRequest request, string errorCode, string errorMessage)
        {
            return new PaymentResponse
            {
                IsSuccess = false,
                PaymentId = $"PAY_FAILED_{Guid.NewGuid():N}",
                TransactionId = string.Empty,
                Status = PaymentStatus.Failed,
                Amount = request.Amount,
                Currency = request.Currency,
                PaymentMethod = request.PaymentMethod,
                ProcessedAt = DateTime.UtcNow,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }

        private static bool IsValidCardNumber(string cardNumber)
        {
            // Remove spaces and dashes
            cardNumber = cardNumber.Replace(" ", "").Replace("-", "");

            // Check if all characters are digits
            if (!Regex.IsMatch(cardNumber, @"^\d+$"))
            {
                return false;
            }

            // Check length (13-19 digits for most card types)
            if (cardNumber.Length < 13 || cardNumber.Length > 19)
            {
                return false;
            }

            // Simple Luhn algorithm check
            return IsValidLuhn(cardNumber);
        }

        private static bool IsValidLuhn(string cardNumber)
        {
            int sum = 0;
            bool alternate = false;

            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int digit = int.Parse(cardNumber[i].ToString());

                if (alternate)
                {
                    digit *= 2;
                    if (digit > 9)
                    {
                        digit = (digit % 10) + 1;
                    }
                }

                sum += digit;
                alternate = !alternate;
            }

            return (sum % 10) == 0;
        }

        private static bool IsValidExpiryDate(string month, string year)
        {
            if (!int.TryParse(month, out int monthValue) || 
                !int.TryParse(year, out int yearValue))
            {
                return false;
            }

            if (monthValue < 1 || monthValue > 12)
            {
                return false;
            }

            var currentDate = DateTime.Now;
            var expiryDate = new DateTime(yearValue, monthValue, 1).AddMonths(1).AddDays(-1);

            return expiryDate >= currentDate.Date;
        }
    }
}
