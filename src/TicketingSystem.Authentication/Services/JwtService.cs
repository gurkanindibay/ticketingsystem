using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TicketingSystem.Shared.Models;

namespace TicketingSystem.Authentication.Services
{
    /// <summary>
    /// JWT service implementation for token generation and validation
    /// </summary>
    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtService> _logger;

        public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Generate JWT access token for the user
        /// </summary>
        public string GenerateAccessToken(User user, IList<string> roles)
        {
            try
            {
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured"));
                var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
                var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");
                var expirationMinutes = int.Parse(jwtSettings["AccessTokenExpirationMinutes"] ?? "15");

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, user.Id),
                    new(ClaimTypes.Name, user.UserName ?? string.Empty),
                    new(ClaimTypes.Email, user.Email ?? string.Empty),
                    new("firstName", user.FirstName),
                    new("lastName", user.LastName),
                    new("isActive", user.IsActive.ToString())
                };

                // Add role claims
                foreach (var role in roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(expirationMinutes),
                    SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                    Issuer = issuer,
                    Audience = audience
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                
                _logger.LogInformation("Generated JWT token for user {UserId}", user.Id);
                return tokenHandler.WriteToken(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating JWT token for user {UserId}", user.Id);
                throw;
            }
        }

        /// <summary>
        /// Generate refresh token
        /// </summary>
        public string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        /// <summary>
        /// Get principal from expired token (for refresh token validation)
        /// </summary>
        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            try
            {
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured"));

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateLifetime = false // We don't validate lifetime for refresh operations
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

                if (securityToken is not JwtSecurityToken jwtSecurityToken || 
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new SecurityTokenException("Invalid token");
                }

                return principal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating expired token");
                return null;
            }
        }

        /// <summary>
        /// Generate HMACSHA512 transaction ID
        /// </summary>
        public string GenerateTransactionId(string data)
        {
            try
            {
                var jwtSettings = _configuration.GetSection("JwtSettings");
                var key = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
                
                using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{data}_{DateTime.UtcNow:yyyyMMddHHmmssfffff}"));
                return Convert.ToHexString(hash).ToLower();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating transaction ID for data: {Data}", data);
                throw;
            }
        }
    }
}
