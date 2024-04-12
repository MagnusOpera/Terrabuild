namespace Api;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;


public class Startup(IConfiguration config)
{

    public void ConfigureServices(IServiceCollection services)
    {
        var sectionSettings = config.GetSection("AppSettings");
        services.Configure<AppSettings>(sectionSettings);
        var appSettings = sectionSettings.Get<AppSettings>()!;

        services.AddSingleton(appSettings);

        services.AddAuthentication()
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(appSettings.Auth.Secret)),
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = appSettings.Auth.Issuer,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services
            .AddCors(options => options.AddDefaultPolicy(builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        services
            .AddSwaggerGen(c => c.SwaggerDoc("v1", new OpenApiInfo { Title = "Api", Version = "v1.0" }))
            .AddControllers();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger().UseSwaggerUI(builder => builder.RoutePrefix = "swagger");
        }

        app.UseAuthentication().UseAuthorization();
        app.UseCors();

        app.UseHttpsRedirection()
            .UseRouting()
            .UseEndpoints(endpoints => endpoints.MapControllers());
    }
}