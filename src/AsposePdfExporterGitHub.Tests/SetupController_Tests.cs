using System;
using Xunit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq.Protected;
using Newtonsoft.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Web;
using Aspose.Cloud.Marketplace.Services;
using Aspose.Cloud.Marketplace.Services.Model.Elasticsearch;
using Microsoft.AspNetCore.Http;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests
{
    public class SetupControllerFixture : ControllerFixture<SetupController>
    {
        public Mock<IHttpClientFactory> HttpFactoryMock;
        public Mock<HttpMessageHandler> HttpMessageHandlerMock;
        public HttpClient HttpClientMock;

        public Mock<ILoggingService> LoggingServiceMock;
        public string GithubEventSecret = "123";
        public override void Initialize()
        {
            HttpFactoryMock = new Mock<IHttpClientFactory>();
            (HttpMessageHandlerMock, HttpClientMock) = CreateGithubHttpClientMock();
            HttpFactoryMock.Setup(e => e.CreateClient("github.com")).Returns(HttpClientMock);

            Configuration = new Dictionary<string, string>()
            {
                {"GithubApp:ClientId", "cliidmock"}
                , {"GithubApp:ClientSecret", "clisecretmock"}
                , {"GithubApp:WebhookEventSecret", GithubEventSecret}
            };

            GitHubClientMock = new GitHubClientMock(mockUser: true);
            ClientServiceMock = new GithubExporterClientServiceMock(GitHubClientMock);

            LoggingServiceMock = new Mock<ILoggingService>();
            LoggingServiceMock.Setup(e => e.ReportSetupLog(It.IsAny<ElasticsearchSetupDocument>())).Returns(Task.CompletedTask);
        }

        public override void ClearInvocations()
        {
            base.ClearInvocations();
            HttpFactoryMock.Invocations.Clear();
            HttpMessageHandlerMock.Invocations.Clear();
        }

        public override IServiceCollection ProvideServices(IServiceCollection c) =>
            base.ProvideServices(c).AddScoped(p => HttpFactoryMock.Object);

        public static (Mock<HttpMessageHandler>, HttpClient) CreateGithubHttpClientMock()
        {
            var githubHttpHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            githubHttpHandlerMock
                .Protected()
                // Setup the PROTECTED method to mock
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                // prepare the expected response of the mocked http call
                .ReturnsAsync(new HttpResponseMessage()
                {
                    StatusCode = HttpStatusCode.OK,
                    //Content = new StringContent("{'access_token':'1'}"),
                    Content = new StringContent("access_token=mock_github_token&token_type=bearer"),
                })
                .Verifiable();
            return (githubHttpHandlerMock, new HttpClient(githubHttpHandlerMock.Object)
                {
                    BaseAddress = new Uri("http://mockgithub.com"),
                }
            );
        }
    }
    [Trait("Controllers", "SetupController")]
    public class SetupController_Tests : IClassFixture<SetupControllerFixture>
    {
        internal SetupControllerFixture Fixture;
        internal SetupController Controller => Fixture.Controller;
        internal GitHubClientMock GitHubClientMock => Fixture.GitHubClientMock;
        internal GithubExporterClientServiceMock ClientServiceMock => Fixture.ClientServiceMock;
        internal Mock<HttpMessageHandler> HttpMessageHandlerMock => Fixture.HttpMessageHandlerMock;
        internal Mock<ILoggingService> LoggingServiceMock => Fixture.LoggingServiceMock;
        internal string GithubEventSecret => Fixture.GithubEventSecret;
        public SetupController_Tests(SetupControllerFixture fixture)
        {
            Fixture = fixture;
            Fixture.ClearInvocations();
        }

        public static ControllerContext SetupRequest(byte[] requestData)
        {
            var request = new Mock<HttpRequest>();
            request.SetupGet(e => e.Body).Returns(new MemoryStream(requestData));
            var context = new Mock<HttpContext>();
            context.SetupGet(x => x.Request).Returns(request.Object);

            return new ControllerContext(new ActionContext
            {
                HttpContext = context.Object,
                RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
                ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
            });
        }

        public static string GithubSignature(string secret, byte[] requestData)
        {
            HMACSHA1 hmac = new HMACSHA1(Encoding.ASCII.GetBytes(secret));
            return $"sha1={string.Concat(hmac.ComputeHash(requestData).Select(x => x.ToString("x2")))}".ToLower();
        }

        internal bool VerifyGetTokenAsyncRequest(HttpRequestMessage req)
        {
            Assert.Equal("application/x-www-form-urlencoded", req.Content.Headers.ContentType.MediaType);
            byte[] contentBytes = typeof(ByteArrayContent).GetField("_content", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(req.Content) as byte[];
            var qs = HttpUtility.ParseQueryString(Encoding.UTF8.GetString(contentBytes));
            Assert.Equal("1", qs["code"]);
            Assert.Equal("2", qs["redirect_uri"]);
            Assert.Equal("3", qs["state"]);
            Assert.Equal("cliidmock", qs["client_id"]);
            Assert.Equal("clisecretmock", qs["client_secret"]);
            return true;
        }

        [Fact]
        public async void GetToken_Test()
        {
            var okResult = await Controller.GetTokenAsync("1", "2", "3", ClientServiceMock) as OkObjectResult;
            Assert.NotNull(okResult);

            var resultStr = JsonConvert.SerializeObject(okResult.Value);
            var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(resultStr);
            Assert.Equal("mock_github_token", result["access_token"]);
            Assert.Equal("bearer", result["token_type"]);
            Assert.Equal(GitHubClientMock.CurrentUserData.Data.Name, result["user_name"]);
            Assert.Equal(GitHubClientMock.CurrentUserEmailData.Data.FirstOrDefault().Email, result["email"]);
            Assert.Equal(GitHubClientMock.CurrentUserData.Data.AvatarUrl, result["avatar_url"]);

            HttpMessageHandlerMock.Protected().Verify("SendAsync", Times.Exactly(1)
                , ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                    && req.RequestUri == new Uri("http://mockgithub.com/login/oauth/access_token")
                    && VerifyGetTokenAsyncRequest(req)
            ), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async void PostWebook_marketplace_purchase_Test()
        {
            var webhookContent = Encoding.UTF8.GetBytes(Properties.Resources.Github_Webhook_purchased);
            Controller.ControllerContext = SetupRequest(webhookContent);
            var result = await Controller.PostWebook("marketplace_purchase", "mock-delivery-id",
                GithubSignature(GithubEventSecret, webhookContent), ClientServiceMock, LoggingServiceMock.Object);
            Assert.NotNull(result as OkResult);
            LoggingServiceMock.Verify(e => e.ReportSetupLog(It.Is<ElasticsearchSetupDocument>( m=>
                m.Action == "marketplace_purchase-purchased" && m.ActionId == "mock-delivery-id" && m.ActionOriginator == "sender-username" && m.Subscriber == "account-username"
                )));
        }

        [Fact]
        public async void PostWebook_ping_Test()
        {
            var webhookContent = Encoding.UTF8.GetBytes(Properties.Resources.Github_Webhook_ping);
            Controller.ControllerContext = SetupRequest(webhookContent);
            var result = await Controller.PostWebook("ping", "mock-delivery-id",
                GithubSignature(GithubEventSecret, webhookContent), ClientServiceMock, LoggingServiceMock.Object);
            Assert.NotNull(result as OkResult);
        }
    }
}
