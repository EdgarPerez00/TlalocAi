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
using System.Net;

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

                var allowPrivateNetworkOrigins =
                    builder.Configuration.GetValue<bool?>("Cors:AllowPrivateNetworkOrigins")
                    ?? builder.Environment.IsDevelopment();

                policy.AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();

                if (allowPrivateNetworkOrigins)
                {
                    policy.SetIsOriginAllowed(origin => IsAllowedOrigin(origin, origins));
                    return;
                }

                policy.WithOrigins(origins);
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

    private static bool IsAllowedOrigin(string? origin, IReadOnlyCollection<string> exactOrigins)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (exactOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.IsLoopback)
        {
            return true;
        }

        if (string.Equals(uri.Host, "host.docker.internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsLocalHostname(uri.Host))
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var ipAddress))
        {
            return false;
        }

        return IsPrivateNetwork(ipAddress);
    }

    private static bool IsPrivateNetwork(IPAddress ipAddress)
    {
        if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            var ipv6Bytes = ipAddress.GetAddressBytes();
            var isUniqueLocal = ipv6Bytes.Length > 0 && (ipv6Bytes[0] & 0xFE) == 0xFC;

            return ipAddress.IsIPv6LinkLocal
                || ipAddress.IsIPv6SiteLocal
                || isUniqueLocal;
        }

        var bytes = ipAddress.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static bool IsLocalHostname(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return !host.Contains('.')
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".lan", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase);
    }
}
