# Authentication Security Guide

## 🛡️ Security Measures Implemented

### 1. **Rate Limiting**
Protects against brute force attacks and DoS attempts.

#### **Rate Limits Configured:**
- **Registration**: 5 attempts per IP per hour
- **Login**: 10 attempts per IP per 15 minutes  
- **Token Refresh**: 20 attempts per IP per 5 minutes

#### **How It Works:**
```csharp
// Example: Registration rate limiting
if (await _rateLimitService.IsRateLimitExceededAsync(clientIp, "register"))
{
    return StatusCode(429, "Too many registration attempts. Please try again later.");
}
```

#### **Storage:**
- **Development**: In-Memory cache (single instance)
- **Production**: Recommended to use Redis for distributed rate limiting

### 2. **Input Validation & Sanitization**

#### **Email Validation:**
```csharp
private static bool IsValidEmail(string email)
{
    try
    {
        var addr = new System.Net.Mail.MailAddress(email);
        return addr.Address == email;
    }
    catch { return false; }
}
```

#### **Password Requirements:**
- Minimum 8 characters
- At least 1 uppercase letter
- At least 1 lowercase letter  
- At least 1 number
- At least 1 special character
- No common passwords

### 3. **Comprehensive Logging**

#### **Security Events Logged:**
- All registration attempts (with IP addresses)
- All login attempts (successful and failed)
- Rate limit violations
- Token refresh attempts
- Failed authentication events

#### **Log Example:**
```
2025-07-19 10:30:15 [WARNING] Rate limit exceeded for registration from IP: 192.168.1.100
2025-07-19 10:31:22 [WARNING] Login failed for email: test@example.com, IP: 10.0.0.5
```

### 4. **IP Address Tracking**
Every authentication request is logged with the client's IP address for:
- Rate limiting by IP
- Security monitoring
- Forensic analysis
- Geolocation-based security

### 5. **Token Security**

#### **Access Tokens:**
- Short lifespan (15 minutes)
- Stateless JWT tokens
- HMAC-SHA256 signed
- Include user claims and roles

#### **Refresh Tokens:**
- Longer lifespan (7 days)
- Stored in database for revocation
- One-time use (token rotation)
- Cryptographically secure random generation

### 6. **Error Handling**
- Generic error messages to prevent information disclosure
- Detailed logging for administrators
- Consistent response format
- No stack traces in production

## 🚨 **Attack Vectors Protected Against**

### **1. Brute Force Attacks**
- **Protection**: Rate limiting per IP address
- **Mitigation**: Progressive delays, account lockouts
- **Monitoring**: Failed login attempt logging

### **2. Credential Stuffing**
- **Protection**: Rate limiting + strong password policies
- **Mitigation**: Monitor for unusual login patterns
- **Detection**: Multiple failed attempts from same IP

### **3. Registration Spam/Abuse**
- **Protection**: Registration rate limiting (5 per hour per IP)
- **Mitigation**: Email verification (future enhancement)
- **Detection**: High registration volume from single IP

### **4. Token Replay Attacks**
- **Protection**: Short-lived access tokens (15 min)
- **Mitigation**: Token rotation on refresh
- **Detection**: Unusual token usage patterns

### **5. Session Fixation**
- **Protection**: New tokens on each refresh
- **Mitigation**: Token rotation prevents session reuse
- **Detection**: Multiple concurrent sessions

### **6. DoS/DDoS Attacks**
- **Protection**: Rate limiting across all endpoints
- **Mitigation**: Progressive backoff strategies
- **Detection**: Abnormal request volume monitoring

## ⚙️ **Configuration**

### **Rate Limiting Settings** (`appsettings.json`):
```json
{
  "RateLimiting": {
    "Registration": {
      "MaxAttempts": 5,
      "WindowMinutes": 60
    },
    "Login": {
      "MaxAttempts": 10,
      "WindowMinutes": 15
    },
    "Refresh": {
      "MaxAttempts": 20,
      "WindowMinutes": 5
    }
  }
}
```

### **JWT Settings**:
```json
{
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLong12345",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

## 📊 **Security Monitoring**

### **Key Metrics to Monitor:**
1. **Failed login attempts per IP/hour**
2. **Registration rate per IP/hour** 
3. **Token refresh frequency per user**
4. **Geographic distribution of requests**
5. **Peak authentication times**

### **Alert Triggers:**
- Rate limit violations (immediate)
- Multiple failed logins from same IP (5+ in 15 min)
- Unusual registration patterns (bulk registrations)
- Token abuse (excessive refresh attempts)

### **Log Analysis Queries:**
```sql
-- Failed login attempts by IP (last hour)
SELECT ClientIP, COUNT(*) as FailedAttempts 
FROM AuthLogs 
WHERE EventType = 'LOGIN_FAILED' AND Timestamp > NOW() - INTERVAL 1 HOUR
GROUP BY ClientIP 
HAVING COUNT(*) >= 5;

-- Registration rate by IP (last 24 hours)  
SELECT ClientIP, COUNT(*) as Registrations
FROM AuthLogs
WHERE EventType = 'REGISTRATION' AND Timestamp > NOW() - INTERVAL 24 HOUR
GROUP BY ClientIP
ORDER BY COUNT(*) DESC;
```

## 🔧 **Additional Security Recommendations**

### **For Production:**

1. **Use Redis for Rate Limiting**:
   ```csharp
   builder.Services.AddScoped<IRateLimitService, RedisRateLimitService>();
   ```

2. **Implement CAPTCHA** for registration after rate limit violations

3. **Add Geo-blocking** for suspicious regions

4. **Enable Account Lockouts** after multiple failed attempts

5. **Add Email Verification** for new registrations

6. **Implement 2FA** for sensitive operations

7. **Use HTTPS Only** in production

8. **Add Request Throttling** at load balancer level

9. **Monitor with SIEM** tools for advanced threat detection

10. **Regular Security Audits** and penetration testing

## 🚀 **Testing Security Measures**

### **Rate Limiting Test:**
```bash
# Test registration rate limit (should fail after 5 attempts)
for i in {1..10}; do
  curl -X POST http://localhost:5000/api/auth/register \
    -H "Content-Type: application/json" \
    -d '{"email":"test'$i'@example.com","password":"TestPass123!","firstName":"Test","lastName":"User"}'
done
```

### **Login Brute Force Test:**
```bash
# Test login rate limit (should fail after 10 attempts)
for i in {1..15}; do
  curl -X POST http://localhost:5000/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"nonexistent@example.com","password":"wrongpassword"}'
done
```

This security implementation provides robust protection while maintaining good user experience for legitimate users.
