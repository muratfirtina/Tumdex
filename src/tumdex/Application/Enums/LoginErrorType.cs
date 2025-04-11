namespace Application.Enums;

public enum LoginErrorType
{
    InvalidCredentials,
    AccountLocked,
    RateLimitExceeded,
    UserNotFound,
    AccountDisabled
}