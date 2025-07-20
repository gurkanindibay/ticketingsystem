using System.ComponentModel.DataAnnotations;

namespace TicketingSystem.Shared.DTOs
{
    /// <summary>
    /// Payment request DTO for ticket purchases
    /// </summary>
    public class PaymentRequest
    {
        [Required]
        [MaxLength(50)]
        public string PaymentMethod { get; set; } = string.Empty; // "credit_card", "debit_card", "paypal", etc.

        [Required]
        [MaxLength(20)]
        public string CardNumber { get; set; } = string.Empty; // Masked/tokenized in real scenario

        [Required]
        [MaxLength(5)]
        public string ExpiryMonth { get; set; } = string.Empty; // MM format

        [Required]
        [MaxLength(5)]
        public string ExpiryYear { get; set; } = string.Empty; // YYYY format

        [Required]
        [MaxLength(10)]
        public string CVV { get; set; } = string.Empty; // Masked in real scenario

        [Required]
        [MaxLength(100)]
        public string CardHolderName { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(3)]
        public string Currency { get; set; } = "USD";

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Payment response DTO from payment gateway
    /// </summary>
    public class PaymentResponse
    {
        public bool IsSuccess { get; set; }
        public string PaymentId { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Payment refund request DTO
    /// </summary>
    public class RefundRequest
    {
        [Required]
        public string PaymentId { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Payment refund response DTO
    /// </summary>
    public class RefundResponse
    {
        public bool IsSuccess { get; set; }
        public string RefundId { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
    }

    /// <summary>
    /// Payment status enumeration
    /// </summary>
    public enum PaymentStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2,
        Failed = 3,
        Cancelled = 4,
        Refunded = 5,
        PartiallyRefunded = 6
    }
}
