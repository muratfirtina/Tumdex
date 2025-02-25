using System.Security.Claims;

namespace Infrastructure.Services.Security.JWT;

public class UserClaimCache
{
    public string Type { get; set; }
    public string Value { get; set; }
    public string ValueType { get; set; }
    public string Issuer { get; set; }
    public string OriginalIssuer { get; set; }

    public static UserClaimCache FromClaim(Claim claim)
    {
        return new UserClaimCache
        {
            Type = claim.Type,
            Value = claim.Value,
            ValueType = claim.ValueType,
            Issuer = claim.Issuer,
            OriginalIssuer = claim.OriginalIssuer
        };
    }

    public Claim ToClaim()
    {
        return new Claim(Type, Value, ValueType, Issuer, OriginalIssuer);
    }
}