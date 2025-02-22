using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace VGen.Backend.Audio;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = "https://accounts.google.com";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = "https://accounts.google.com",
                    ValidateAudience = true,
                    ValidAudience = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID"),
                    ValidateLifetime = true
                };
            });
    }
}