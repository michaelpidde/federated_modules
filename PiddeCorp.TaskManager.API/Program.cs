using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PiddeCorp.EnterpriseLogging;
using PiddeCorp.TaskManager.API.Repositories;

const string GatewayIssuer = "piddecorp-gateway";
const string TaskManagerAudience = "taskmanager-api";
const string GatewayPublicKeyRelativePath = "keys/PiddeCorp.Gateway.Public.pem";

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddEnterpriseLogging(o => {
        o.ServiceName = "PiddeCorp.TaskManager.API";
        o.ForwardToProviders = true; // so Aspire ServiceDefaults' OTel logging provider gets events
    }
);
builder.AddServiceDefaults();

RSA gatewayRsa = RSA.Create();
gatewayRsa.ImportFromPem(
    File.ReadAllText(Path.Combine(builder.Environment.ContentRootPath, GatewayPublicKeyRelativePath))
);
RsaSecurityKey gatewaySigningKey = new(gatewayRsa);

builder.Services.AddControllers();
builder.Services.AddScoped<TaskRepository>();

builder.Services
       .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
       .AddJwtBearer(options => {
               options.TokenValidationParameters = new TokenValidationParameters {
                   ValidateIssuer = true,
                   ValidIssuer = GatewayIssuer,
                   ValidateAudience = true,
                   ValidAudience = TaskManagerAudience,
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
                          policy => policy.WithOrigins("http://localhost:5173")
                                          .AllowAnyMethod()
                                          .AllowAnyHeader()
        );
    }
);

WebApplication app = builder.Build();

app.UseEnterpriseLogging();
app.MapDefaultEndpoints();

app.UseCors("DevPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/tasks", (TaskRepository repo) => repo.GetOpenTasks()).RequireAuthorization();

app.Run();
