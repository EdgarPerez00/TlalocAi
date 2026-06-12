using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

namespace TlalocAi.ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    public static WebApplicationBuilder AddTlalocServiceDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Service", serviceName)
                .WriteTo.Console();
        });

        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks();
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("frontend", policy =>
            {
                var origins = builder.Configuration.GetSection("Cors:AllowedOrigins")
                    .GetChildren()
                    .Select(value => value.Value)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .ToArray();

                if (origins.Length == 0)
                {
                    origins = ["http://localhost:5173", "http://localhost:3000"];
                }

                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var signingKey = builder.Configuration["Jwt:SigningKey"]
                    ?? "development-signing-key-change-this-value-please";

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "TlalocAi",
                    ValidAudience = builder.Configuration["Jwt:Audience"] ?? "TlalocAi.Frontend",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ClockSkew = TimeSpan.FromMinutes(2)
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = serviceName, Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            options.AddSecurityDefinition("DeviceApiKey", new OpenApiSecurityScheme
            {
                Name = builder.Configuration["DeviceAuth:ApiKeyHeaderName"] ?? "X-Device-Api-Key",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey
            });
        });

        return builder;
    }

    public static WebApplication UseTlalocServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();
        app.UseCors("frontend");

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health", new HealthCheckOptions());
        return app;
    }
}
