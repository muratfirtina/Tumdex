using System.Text;
using System.Text.Json;
using Application.Abstraction.Services.Utilities;
using MediatR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Application.Features.Tokens.Command.VerifyActivationToken;

public class VerifyActivationTokenCommand : IRequest<VerifyActivationTokenResponse>
    {
        public string Token { get; set; }
        
        public class VerifyActivationTokenCommandHandler : 
            IRequestHandler<VerifyActivationTokenCommand, VerifyActivationTokenResponse>
        {
            private readonly IEncryptionService _encryptionService;
            private readonly ILogger<VerifyActivationTokenCommandHandler> _logger;
            
            public VerifyActivationTokenCommandHandler(
                IEncryptionService encryptionService, 
                ILogger<VerifyActivationTokenCommandHandler> logger)
            {
                _encryptionService = encryptionService;
                _logger = logger;
            }
            
            public async Task<VerifyActivationTokenResponse> Handle(VerifyActivationTokenCommand request, CancellationToken cancellationToken)
            {
                try
                {
                    // Decode token
                    string decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
                    
                    // Decrypt token content
                    string decryptedJson = await _encryptionService.DecryptAsync(decodedToken);
                    
                    // Parse token data
                    JsonDocument tokenData = JsonDocument.Parse(decryptedJson);
                    
                    // Extract data
                    string userId = "";
                    string email = "";
                    
                    if (tokenData.RootElement.TryGetProperty("userId", out var userIdProp))
                        userId = userIdProp.GetString();
                    else if (tokenData.RootElement.TryGetProperty("userId", out userIdProp))
                        userId = userIdProp.GetString();
                        
                    if (tokenData.RootElement.TryGetProperty("email", out var emailProp))
                        email = emailProp.GetString();
                    else if (tokenData.RootElement.TryGetProperty("email", out emailProp))
                        email = emailProp.GetString();
                        
                    if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
                    {
                        _logger.LogWarning("Token valid but does not contain required fields: {JSON}", decryptedJson);
                        return new VerifyActivationTokenResponse 
                        { 
                            Success = false, 
                            Message = "Invalid token content" 
                        };
                    }
                    
                    // Check expiration
                    if (tokenData.RootElement.TryGetProperty("expires", out var expiryProp))
                    {
                        long expiryTimestamp = expiryProp.GetInt64();
                        DateTimeOffset expiryTime = DateTimeOffset.FromUnixTimeSeconds(expiryTimestamp);
                        
                        if (expiryTime < DateTimeOffset.UtcNow)
                        {
                            return new VerifyActivationTokenResponse 
                            { 
                                Success = false, 
                                Message = "Token has expired" 
                            };
                        }
                    }
                    
                    // Token is valid
                    return new VerifyActivationTokenResponse
                    {
                        Success = true,
                        UserId = userId,
                        Email = email
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Token content could not be resolved: {Message}", ex.Message);
                    return new VerifyActivationTokenResponse
                    {
                        Success = false,
                        Message = "Invalid or expired token"
                    };
                }
            }
        }
    }