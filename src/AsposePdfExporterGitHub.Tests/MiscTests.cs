using System;
using System.Collections.Generic;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Text.Encodings.Web;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests
{
    [Trait("Handler", "Handler_Tests")]
    public class Handler_Tests
    {
        private AccessTokenAuthenticationHandler _token_handler;
        public Handler_Tests()
        {
            var mockLogger = new Mock<ILogger<Handler_Tests>>();
            mockLogger.Setup(
                m => m.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.IsAny<object>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<object, Exception, string>>()));

            var mockLoggerFactory = new Mock<ILoggerFactory>();
            mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(() => mockLogger.Object);


            var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
            options.Setup(e => e.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());
            var encoder = new Mock<UrlEncoder>();
            var clock = new Mock<ISystemClock>();
            _token_handler = new AccessTokenAuthenticationHandler(options.Object, mockLoggerFactory.Object, encoder.Object, clock.Object);
        }

        [Fact]
        public async void TokenHandler_Test()
        {
            var _context = new DefaultHttpContext();
            _context.Request.Headers.Add(HeaderNames.Authorization, "Token 123");
            await _token_handler.InitializeAsync(new AuthenticationScheme("default", "Mock", typeof(AccessTokenAuthenticationHandler)), _context);

            var result = await _token_handler.AuthenticateAsync();

            Assert.True(result.Succeeded);
            Assert.Equal("123", result.Ticket.Principal.Claims.FirstOrDefault(c => c.Type == "Authorization")?.Value);
            Assert.Equal("John Doe", result.Ticket.Principal.Identity.Name);
        }

        [Fact]
        public async void EmptyToken_Test()
        {
            var _context = new DefaultHttpContext();
            _context.Request.Headers.Add(HeaderNames.Authorization, "Token");
            await _token_handler.InitializeAsync(new AuthenticationScheme("default", "Mock", typeof(AccessTokenAuthenticationHandler)), _context);

            var result = await _token_handler.AuthenticateAsync();

            Assert.False(result.Succeeded);
        }

        [Fact]
        public async void NoAuthHeader_Test()
        {
            var _context = new DefaultHttpContext();
            await _token_handler.InitializeAsync(new AuthenticationScheme("default", "Mock", typeof(AccessTokenAuthenticationHandler)), _context);

            var result = await _token_handler.AuthenticateAsync();

            Assert.False(result.Succeeded);
        }
    }


    [Trait("IssueData", "IssueData_Tests")]
    public class IssueData_Tests
    {
        private GithubExporterClientServiceMock _cli_mock;
        public IssueData_Tests()
        {
            _cli_mock = new GithubExporterClientServiceMock(new GitHubClientMock(mockRepositories:true, mockIssueData:true));
        }

        [Fact]
        public async void IssueData_Test()
        {
            var r = _cli_mock.GitHubClientMock.RepositoriesData.Data.FirstOrDefault();
            var i = _cli_mock.GitHubClientMock.RepositoryIssuesData[r.Id].Data.FirstOrDefault();
            var result = await IssueData.Fetch(_cli_mock.GitHubClientMock.MockGitHubClient.Object, r.Id, i.Number);
            Assert.NotNull(result);
            Assert.Equal(i.State.StringValue, result.Issue.State.StringValue);
        }
    }

    [Trait("ReportGithubModel", "ReportGithubModel_Tests")]
    public class ReportGithubModel_Tests
    {
        private GithubExporterClientServiceMock _cli_mock;
        public ReportGithubModel_Tests()
        {
            _cli_mock = new GithubExporterClientServiceMock(new GitHubClientMock(mockRepositories: true, mockIssueData: true, mockComments:true));
        }

        [Fact]
        public async void IssueData_Test()
        {
            ReportGithubModel m = new ReportGithubModel("");

            var r = _cli_mock.GitHubClientMock.RepositoriesData.Data.FirstOrDefault();
            var i = _cli_mock.GitHubClientMock.RepositoryIssuesData[r.Id].Data.FirstOrDefault();

            var result = m.issuesModel(new List<IssueData>() { await IssueData.Fetch(_cli_mock.GitHubClientMock.MockGitHubClient.Object, r.Id, i.Number) } );
            Assert.NotNull(result);
            Assert.NotEmpty(result.issues);
            var issues = result.issues as IEnumerable<dynamic>;
            Assert.Equal(i.Number.ToString(), issues.ElementAt(0).issueName.ToString());
            Assert.Equal(i.State.StringValue, issues.ElementAt(0).state.ToString());
            Assert.Equal(i.Assignee.Login, issues.ElementAt(0).assignee.ToString());
        }
    }
}
