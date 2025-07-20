# Mock Payment Service Documentation

## Overview

The Mock Payment Service simulates a real payment gateway for development and testing purposes. It provides realistic payment processing behavior including validation, different payment outcomes, and error scenarios.

## Features

### Payment Processing
- **Card Validation**: Luhn algorithm validation for card numbers
- **Expiry Date Validation**: Ensures cards are not expired
- **CVV Validation**: Basic format validation (3-4 digits)
- **Multiple Payment Outcomes**: Success, failure, processing states
- **Transaction ID Generation**: Using HMACSHA512 as per system requirements

### Test Card Numbers

The service includes predefined test card numbers that simulate different payment scenarios:

| Card Number | Outcome | Description |
|-------------|---------|-------------|
| `4111111111111111` | ✅ Success | Standard successful payment |
| `4242424242424242` | ✅ Success | Alternative successful payment |
| `4000000000000002` | ❌ Failed | Declined by bank |
| `4000000000000119` | ⏳ Processing | Payment still processing |
| `4000000000000127` | ❌ Failed | Insufficient funds |
| Other valid cards | ✅ Success (90%) | Random outcome with 90% success rate |

### Error Handling

The service provides comprehensive error handling with specific error codes:

- `VALIDATION_FAILED`: Invalid payment request data
- `PAYMENT_DECLINED`: Payment declined by issuing bank
- `PAYMENT_PROCESSING`: Payment is still being processed
- `PAYMENT_CANCELLED`: Payment was cancelled
- `PROCESSING_ERROR`: General processing error

## API Methods

### ProcessPaymentAsync
Processes a payment request and returns a comprehensive payment response.

**Input**: `PaymentRequest`
- Payment method, card details, amount, currency
- Validates all required fields
- Supports amounts from $0.01 to unlimited

**Output**: `PaymentResponse`
- Payment ID, transaction ID, status, timestamps
- Metadata including processing time and card info
- Error details if payment fails

### ProcessRefundAsync
Handles refund requests for previously successful payments.

**Features**:
- Validates original payment exists and was successful
- Prevents refunds exceeding original amount
- Supports partial refunds
- Generates unique refund IDs

### GetPaymentStatusAsync
Retrieves the current status of a payment by payment ID.

**Use Cases**:
- Check payment completion status
- Verify payment details
- Audit payment history

### ValidatePaymentRequest
Validates payment request data before processing.

**Validations**:
- Required field checks
- Card number format (Luhn algorithm)
- Expiry date validation
- CVV format validation
- Amount validation (positive values)

## Integration with Ticket Service

The payment service is integrated into the ticket purchasing flow:

1. **Ticket Purchase Request**: User provides payment details with ticket request
2. **Event Availability Check**: Verify tickets are available
3. **Payment Amount Calculation**: Calculate total based on ticket price and quantity
4. **Payment Processing**: Process payment through mock payment service
5. **Transaction Creation**: Create ticket transaction with HMACSHA512 transaction ID
6. **Ticket Generation**: Generate tickets on successful payment
7. **Capacity Update**: Update event capacity via RabbitMQ (asynchronous)

## Mock Data and Storage

The service uses in-memory storage for development:
- `_paymentStore`: Stores all processed payments
- `_refundStore`: Stores all processed refunds
- Data persists for the lifetime of the application instance

## Testing Scenarios

### Successful Purchase Flow
```json
{
  "eventId": 1,
  "eventDate": "2025-08-20T20:00:00Z",
  "quantity": 2,
  "paymentDetails": {
    "paymentMethod": "credit_card",
    "cardNumber": "4111111111111111",
    "expiryMonth": "12",
    "expiryYear": "2026",
    "cvv": "123",
    "cardHolderName": "John Doe",
    "currency": "USD"
  }
}
```

### Failed Payment Flow
```json
{
  "paymentDetails": {
    "cardNumber": "4000000000000002"
    // ... other details
  }
}
```

### Invalid Card Flow
```json
{
  "paymentDetails": {
    "cardNumber": "1234567890123456"
    // ... other details
  }
}
```

## Security Considerations

### Development Environment
- Card numbers are stored in plain text for testing
- No actual financial data is processed
- Payment IDs are generated using secure random GUIDs

### Production Recommendations
1. **Replace with Real Payment Gateway**: Integrate with Stripe, PayPal, or similar
2. **PCI DSS Compliance**: Never store actual card details
3. **Tokenization**: Use payment gateway tokens instead of raw card data
4. **Encryption**: Encrypt all sensitive payment data in transit and at rest
5. **Audit Logging**: Log all payment attempts for security monitoring
6. **Rate Limiting**: Implement payment-specific rate limiting
7. **Fraud Detection**: Add fraud detection and prevention mechanisms

## Configuration

### Environment Variables
The service can be configured through environment variables:
- `PAYMENT_SUCCESS_RATE`: Default success rate for unknown cards (default: 90%)
- `PAYMENT_PROCESSING_DELAY_MIN`: Minimum processing delay in ms (default: 500)
- `PAYMENT_PROCESSING_DELAY_MAX`: Maximum processing delay in ms (default: 2000)

### Logging
The service provides comprehensive logging:
- Payment attempts and outcomes
- Validation failures
- Error conditions
- Performance metrics

## Future Enhancements

1. **Real Payment Gateway Integration**: Replace mock with actual payment providers
2. **Multiple Payment Methods**: Support for PayPal, Apple Pay, Google Pay
3. **Currency Conversion**: Support for multiple currencies
4. **Recurring Payments**: Support for subscription-based ticketing
5. **Payment Analytics**: Detailed payment reporting and analytics
6. **Webhook Support**: Real-time payment status notifications
7. **3D Secure**: Enhanced security for credit card transactions
