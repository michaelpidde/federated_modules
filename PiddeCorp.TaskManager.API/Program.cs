using Microsoft.AspNetCore.Authentication.JwtBearer;
using PiddeCorp.EnterpriseLogging;
using PiddeCorp.Gateway.Auth;
using PiddeCorp.TaskManager.API.Repositories;


WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddEnterpriseLogging(o => {
        o.ServiceName = "PiddeCorp.TaskManager.API";
        o.ForwardToProviders = true; // so Aspire ServiceDefaults' OTel logging provider gets events
    }
);
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddScoped<TaskRepository>();

if(builder.Environment.IsDevelopment()) {
    // POC: accept the Gateway's fake Okta token. See PiddeCorp.Gateway/Auth/FakeOktaBearer.cs.
    builder.Services
           .AddAuthentication(FakeOktaBearerExtensions.SchemeName)
           .AddFakeOktaBearer();
} else {
    // Placeholder for the real thing: validate Okta-issued JWTs. Okta:Authority and
    // Okta:Audience aren't configured anywhere yet, so outside Development every
    // authenticated request is rejected (fail closed).
    builder.Services
           .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
           .AddJwtBearer(options => {
                   options.Authority = builder.Configuration["Okta:Authority"];
                   options.Audience = builder.Configuration["Okta:Audience"];
               }
           );
}

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
