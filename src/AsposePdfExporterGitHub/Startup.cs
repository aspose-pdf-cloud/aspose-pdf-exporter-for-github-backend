using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Logging;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Aspose.Cloud.Marketplace.App.Middleware;
using Aspose.Cloud.Marketplace.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            CheckConfig(Configuration);

            services.AddCors();
            services.AddControllers();
            services.AddHttpContextAccessor();
            services.AddHttpClient();
            // Add HttpClient used in Setup controller to access Github oauth API
            services.AddHttpClient("github.com", c =>
            {
                c.BaseAddress = new Uri("https://github.com");
            });

            // we want to use our AccessTokenAuthenticationHandler implementation
            services.AddAuthentication("AccessTokenAuthentication")
                .AddScheme<AuthenticationSchemeOptions, AccessTokenAuthenticationHandler>("AccessTokenAuthentication", null);
            
            // add ClaimsPrincipal service that resolves to the current user
            services.AddTransient(s => s.GetService<IHttpContextAccessor>().HttpContext.User);
            // add configuration expression
            services.AddSingleton<IConfigurationExpression, ConfigurationExpression>();
            // add Url base path replacement service
            services.AddSingleton<IBasePathReplacement>(provider =>
            {
                var configuration = provider.GetRequiredService<IConfiguration>();
                return new BasePathReplacementService(configuration.GetValue<string>("Settings:BaseAppUrl"));
            });
            //Create application service
            services.AddScoped<Services.IAppGithubExporterCli>(provider => {
                var user = provider.GetService<ClaimsPrincipal>();
                var configuration = provider.GetRequiredService<IConfiguration>();
                var hostEnvironment = provider.GetRequiredService<IWebHostEnvironment>();
                
                return new Services.GithubExporterClientService(configuration.GetValue<string>("Settings:AppName"),
                    user.Claims.FirstOrDefault(c => c.Type == "Authorization")?.Value,
                    configuration.GetValue<string>("AsposeCloud:ApiKey"), configuration.GetValue<string>("AsposeCloud:AppSid"), configuration.GetValue<string>("AsposeCloud:BasePath", null),
                    hostEnvironment.IsDevelopment());
            });

            // Create ILoggingService to server Elasticsearch logging
            services.AddScoped<ILoggingService>(provider => {
                var configExpression = provider.GetRequiredService<IConfigurationExpression>();
                var hostEnvironment = provider.GetRequiredService<IWebHostEnvironment>();
                var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

                return new ElasticsearchLoggingService(loggerFactory.CreateLogger<ElasticsearchLoggingService>()
                    , Configuration.GetSection("Elasticsearch:Uris")?.Get<string[]>()
                    , configExpression.Get("Elasticsearch:ErrorlogIndex", "errorlog-{DateTime.Now.ToString(\"yyyy.MM.dd\")")
                    , configExpression.Get("Elasticsearch:AccesslogIndex", "accesslog-{DateTime.Now.ToString(\"yyyy.MM.dd\")")
                    , setuplogIndexName: configExpression.Get("Elasticsearch:SetuplogIndex", "setuplog-{DateTime.Now.ToString(\"yyyy.MM.dd\")")
                    , apiId: configExpression.Get("Elasticsearch:apiId"), apiKey: configExpression.Get("Elasticsearch:apiKey")
                    , timeoutSeconds: 5
                    , debug: hostEnvironment.IsDevelopment());
            });
            
            services.AddHealthChecks();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                IdentityModelEventSource.ShowPII = true;
            }
            
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseCors(builder => builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
            );

            app.UseStaticFiles();

            // Initialization order of those middleware matters!
            if (Configuration.GetValue("Settings:UseAccessLogMiddleware", true))
                app.UseMiddleware<ElasticsearchAccessLogMiddleware<Services.IAppGithubExporterCli>>();

            if (Configuration.GetValue("Settings:UseExceptionMiddleware", true))
                app.UseMiddleware<StoreExceptionHandlingMiddleware<Services.IAppGithubExporterCli>>();

            
            string gitlabAuthority = "https://github.com";

            // Prepare configuration structure used by frontend
            var configJson = JsonConvert.SerializeObject(new
            {
                Authentication = new
                {
                    AuthorizationEndpoint = $"{gitlabAuthority}/login/oauth/authorize",
                    TokenEndpoint = $"{gitlabAuthority}/login/oauth/access_token",
                    UserInformationEndpoint = $"https://api.github.com/user",
                    ClientId = Configuration.GetValue<string>("GithubApp:ClientId"),
                }
            });

            app.UseHealthChecks("/status", new HealthCheckOptions()
            {
                ResponseWriter = async (context, report) =>
                {
                    context.Response.ContentType = "application/json";
                    string result = "Healthy";
                    switch (report.Status)
                    {
                        case Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy: result = "Failure"; break;
                        case Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded: result = "Degraded"; break;
                    }
                    await context.Response.WriteAsync(result);
                }
            });


            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                // config for frontend
                endpoints.MapGet("config.json", async context =>
                {
                    await context.Response.WriteAsync(configJson);
                });
                
                endpoints.MapFallbackToFile("index.html");
            });

            
        }
        internal void CheckConfig(IConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config.GetValue<string>("Settings:AppName")))
                throw new ArgumentException("Application name is not specified", "Settings:AppName");
            if (string.IsNullOrWhiteSpace(config.GetValue<string>("AsposeCloud:ApiKey")) ||
                string.IsNullOrWhiteSpace(config.GetValue<string>("AsposeCloud:AppSid")))
                throw new ArgumentException("Aspose.Cloud's AppSid/AppKey were not specified. You can obtain them at https://dashboard.aspose.cloud", "AppSid/AppKey");
            if (string.IsNullOrWhiteSpace(config.GetValue<string>("GithubApp:ClientId")) ||
                string.IsNullOrWhiteSpace(config.GetValue<string>("GithubApp:ClientSecret")))
                throw new ArgumentException("Github's ClientId/ClientSecret were not specified. You can obtain them at https://github.com/settings/applications/new", "ClientId/ClientSecret");
        }

    }
}
