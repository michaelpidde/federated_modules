using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

const string GatewayIssuer = "piddecorp-gateway";
const string Module2Audience = "module2-api";
const string GatewayPublicKeyRelativePath = "keys/PiddeCorp.Gateway.Public.pem";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

RSA gatewayRsa = RSA.Create();
gatewayRsa.ImportFromPem(
    File.ReadAllText(Path.Combine(builder.Environment.ContentRootPath, GatewayPublicKeyRelativePath))
);
RsaSecurityKey gatewaySigningKey = new(gatewayRsa);

builder.Services.AddControllers();

builder.Services
       .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(options => {
               options.TokenValidationParameters = new TokenValidationParameters {
                   ValidateIssuer = true,
                   ValidIssuer = GatewayIssuer,
                   ValidateAudience = true,
                   ValidAudience = Module2Audience,
                   ValidateLifetime = true,
                   ValidateIssuerSigningKey = true,
                   IssuerSigningKey = gatewaySigningKey,
                   // The internal token is only valid for 30 seconds to begin with -
                   // this just accounts for clock drift between Gateway and this module.
                   ClockSkew = TimeSpan.FromSeconds(5),
               };
           }
       );

builder.Services.AddAuthorization();

// CORS for local dev - allows Vite dev server
builder.Services.AddCors(options => {
        options.AddPolicy("DevPolicy",
                          policy => policy.WithOrigins("http://localhost:5176")
                                          .AllowAnyMethod()
                                          .AllowAnyHeader()
        );
    }
);

WebApplication app = builder.Build();

app.UseCors("DevPolicy");

app.UseAuthentication();
app.UseAuthorization();

const string garbage = """
                   ___
               ,--[___]--,
              /           \
             |,.--'```'--.,|    ,
             |'-.,_____,.-'|    ||
             |'-.,_____,.-'|    ||
             |             |   _||_
             |   GARBAGE   |  ///\\\
             |             |  HHHHHH
             |'-.,_____,.-'|  ||||||
             `'-.,_____,.-''  ||||||
             """;

app.MapGet("/api/garbage", () => Results.Json(garbage)).RequireAuthorization();

app.Run();
