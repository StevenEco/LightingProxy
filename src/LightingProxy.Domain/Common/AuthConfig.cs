using LightingProxy.Domain.Enums;

namespace LightingProxy.Domain.Common;

public class AuthConfig
{
    public AuthMethod Method { get; set; } = AuthMethod.Token;

    public string? Token { get; set; }

    public OidcConfig? Oidc { get; set; }
}

public class OidcConfig
{
    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    public bool SkipExpiryCheck { get; set; }

    public bool SkipIssuerCheck { get; set; }
}
