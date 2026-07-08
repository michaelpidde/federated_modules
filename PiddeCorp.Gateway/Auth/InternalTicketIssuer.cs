using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace PiddeCorp.Gateway.Auth;

// Mints the short-lived internal token the Gateway attaches to every proxied
// request into a module BFF. This is a DIFFERENT keypair and a DIFFERENT
// trust boundary than the LegacySite relay token: that one crosses the
// browser and needs replay protection (see ADR-003). This one never leaves
// the private network - Gateway to module BFF only - so there's no jti
// replay store here. jti is still included because it costs nothing and
// gives a module something to correlate in its own logs, but the actual
// control stopping misuse of this token is network isolation (see the
// network topology diagram/ADR), not a replay guard. If that assumption
// ever changes - if a module BFF becomes reachable some other way - this
// needs a replay store too.
public class InternalTokenIssuer(RsaSecurityKey signingKey) {
    private const string Issuer = "piddecorp-gateway";
    private const int TokenLifetimeSeconds = 30;
    
    // audience identifies which module this token is for - "taskmanager-api",
    // etc. Each module BFF only accepts tokens where aud matches itself, so a
    // token minted for one module can't be replayed against another even if
    // network isolation were ever misconfigured.
    public string IssueToken(string userId, string name, string audience) {
        Claim[] claims = [
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", name),
        ];
        
        SigningCredentials credentials = new(signingKey, SecurityAlgorithms.RsaSha256);
        
        JwtSecurityToken token = new(
            issuer: Issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddSeconds(TokenLifetimeSeconds),
            signingCredentials: credentials
        );
        
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
