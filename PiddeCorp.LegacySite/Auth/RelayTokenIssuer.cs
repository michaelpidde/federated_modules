using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace PiddeCorp.LegacySite.Auth;

// Signs the short-lived, single-use relay token that gets POSTed to the
// Gateway's /auth/external-login endpoint. This class holds the ONLY copy of
// the private key in this whole pilot - it never leaves the server, and
// definitely never touches anything that renders in a browser. See ADR-003,
// decision 2.
public class RelayTokenIssuer {
    private const string Issuer = "legacysite";
    private const string Audience = "piddecorp-gateway";
    private const int TokenLifetimeSeconds = 30; // only needs to survive one redirect hop
    
    private readonly RsaSecurityKey _signingKey;
    
    public RelayTokenIssuer(IWebHostEnvironment env) {
        RSA rsa = RSA.Create();
        string keyPath = Path.Combine(env.ContentRootPath, "keys", "PiddeCorp.LegacySite.Private.pem");
        rsa.ImportFromPem(File.ReadAllText(keyPath));
        _signingKey = new RsaSecurityKey(rsa);
    }
    
    public string IssueRelayToken(string userId, string name) {
        Claim[] claims = [
            new(JwtRegisteredClaimNames.Sub, userId), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", name),
        ];
        
        SigningCredentials credentials = new(_signingKey, SecurityAlgorithms.RsaSha256);
        
        JwtSecurityToken token = new(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddSeconds(TokenLifetimeSeconds),
            signingCredentials: credentials
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
