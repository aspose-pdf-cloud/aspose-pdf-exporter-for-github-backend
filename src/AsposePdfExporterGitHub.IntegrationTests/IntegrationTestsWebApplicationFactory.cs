using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Services;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks;
using Aspose.Cloud.Marketplace.Report;
using Aspose.Cloud.Marketplace.Services;
using Aspose.Cloud.Marketplace.Services.Model.Elasticsearch;
using Aspose.Pdf.Cloud.Sdk.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.IntegrationTests
{
    /// <summary>
    /// Custom WebApplicationFactory to produce mocks for application created by TestServer
    ///
    /// https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-3.1#customize-webapplicationfactory
    /// </summary>
    /// <typeparam name="TStartup"></typeparam>
    public class IntegrationTestsWebApplicationFactory<TStartup>
        : WebApplicationFactory<TStartup> where TStartup : class
    {
        /// <summary>
        /// AuthenticationHandler to build mocker AuthenticationTicket
        /// </summary>
        public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
                ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
                : base(options, logger, encoder, clock)
            {
            }

            protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                string accessToken = null;
                try
                {
                    var authHeaderValue = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);

                    Regex regexp = new Regex(@"\S+\s+(?<token>\S+)", RegexOptions.IgnoreCase);
                    System.Text.RegularExpressions.Match match = regexp.Match(authHeaderValue.ToString());
                    if (match.Success)
                        accessToken = match.Groups["token"].Value;
                }
                catch
                {
                    return AuthenticateResult.NoResult();
                }

                if (accessToken == null)
                    return AuthenticateResult.NoResult();

                var claims = new[] {new Claim(ClaimTypes.Name, "Test user"), new Claim("Authorization", accessToken)};
                var identity = new ClaimsIdentity(claims, "Test");
                var principal = new ClaimsPrincipal(identity);
                var ticket = new AuthenticationTicket(principal, "Test");

                var result = AuthenticateResult.Success(ticket);

                return result;
            }
        }

        public Mock<ILoggingService> LoggingMock;
        public Dictionary<string, string> ConfigMock;
        public GitHubClientMock GitHubClientMock;
        public GithubExporterClientServiceMock AppMock;
        public Mock<IPdfApi> PdfApiMock;
        public Mock<IBarcodeApi> BarcodeApiMock;

        public Mock<IHttpClientFactory> HttpFactoryMock;
        public Mock<HttpMessageHandler> GithubHttpMessageHandlerMock;
        public HttpClient GithubHttpClientMock;


        public IntegrationTestsWebApplicationFactory()
        {
            // setup ILoggingService mocks
            LoggingMock = new Mock<ILoggingService>();

            LoggingMock.Setup(e => e.ReportAccessLog(It.IsAny<ElasticsearchAccessLogDocument>())).Returns(Task.CompletedTask);
            LoggingMock.Setup(e => e.ReportErrorLog(It.IsAny<ElasticsearchErrorDocument>())).Returns(Task.CompletedTask);
            LoggingMock.Setup(e => e.ReportSetupLog(It.IsAny<ElasticsearchSetupDocument>())).Returns(Task.CompletedTask);

            // setup Config mocks
            ConfigMock = new Dictionary<string, string>()
            {
                {"Settings:StorageRoot", "mockroot"},
                {"GithubApp:WebhookEventSecret", "123"}
            };

            // setup IGitHubClient mocks
            GitHubClientMock = new GitHubClientMock(mockAll: true);

            // Mock everything we need in IPdfApi
            PdfApiMock = PdfExporter.Tests.PdfReport_Tests.Setup(PdfExporter.Tests.PdfReportPageProcessorFixture.Setup(new Mock<IPdfApi>()));
            BarcodeApiMock = PdfExporter.Tests.PdfReportPageProcessorFixture.Setup(new Mock<IBarcodeApi>());
            // setup IAppGithubExporterCli mock
            AppMock = new GithubExporterClientServiceMock(GitHubClientMock, PdfApiMock, BarcodeApiMock);

            //setup HttpClient mocks to perform /token calls for github.com
            HttpFactoryMock = new Mock<IHttpClientFactory>();
            (GithubHttpMessageHandlerMock, GithubHttpClientMock) = SetupControllerFixture.CreateGithubHttpClientMock();
            HttpFactoryMock.Setup(e => e.CreateClient("github.com")).Returns(GithubHttpClientMock);
        }

        public void ClearInvocations()
        {
            GitHubClientMock.ClearInvocations();
            LoggingMock.Invocations.Clear();
            PdfApiMock.Invocations.Clear();
            BarcodeApiMock.Invocations.Clear();
            HttpFactoryMock.Invocations.Clear();
            GithubHttpMessageHandlerMock.Invocations.Clear();
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                conf.AddInMemoryCollection(ConfigMock.ToList());
            })
            .ConfigureServices(services =>
            {
                // Mock authentication
                services.AddAuthentication("TestAuthentication")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestAuthentication", null);

                // Replace IAppGithubExporterCli with mocked version
                services.Replace(ServiceDescriptor.Scoped<IAppGithubExporterCli>(p => AppMock));

                // Replace ILoggingService with mocked version
                services.Replace(ServiceDescriptor.Scoped(p => LoggingMock.Object));

                // Replace IHttpClientFactory with mocked version
                services.Replace(ServiceDescriptor.Scoped(p => HttpFactoryMock.Object));
            });
        }
    }
}
