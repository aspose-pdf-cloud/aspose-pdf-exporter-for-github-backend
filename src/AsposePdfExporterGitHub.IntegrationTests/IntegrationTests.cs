using System.Collections.Generic;
using Xunit;
using Moq;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks;
using Aspose.Cloud.Marketplace.Report;
using Aspose.Cloud.Marketplace.Services;
using Aspose.Cloud.Marketplace.Services.Model.Elasticsearch;
using Aspose.Pdf.Cloud.Sdk.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.IntegrationTests
{
    /// <summary>
    /// Integration tests uses custom WebApplicationFactory to create test server
    /// then we just use HttpClients provided by WebApplicationFactory to perform REST API calls
    /// </summary>
    [Trait("Integration", "Integration_Tests")]
    public class Integration_Tests : IClassFixture<IntegrationTestsWebApplicationFactory<Startup>>
    {
        internal IntegrationTestsWebApplicationFactory<Startup> Factory;
        internal Mock<ILoggingService> LoggingMock => Factory.LoggingMock;
        internal GitHubClientMock GitHubClientMock => Factory.GitHubClientMock;
        internal Mock<IPdfApi> PdfApiMock => Factory.PdfApiMock;
        internal Mock<IBarcodeApi> BarcodeApiMock => Factory.BarcodeApiMock;
        internal readonly HttpClient Client, AuthClient;
        private readonly ITestOutputHelper Output;
        public Integration_Tests(IntegrationTestsWebApplicationFactory<Startup> factory, ITestOutputHelper output)
        {
            Factory = factory;
            Output = output;

            Factory.ClearInvocations();
            
            Client = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            AuthClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
            AuthClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("TestScheme", "mock_123");
        }

        internal static void EqualExpected(dynamic expected, string actual, int? pageNo = null)
        {
            var expectedResult = new
            {
                result = expected,
                pageNo = pageNo
            };
            var expectedStr = JsonConvert.SerializeObject(expectedResult);
            Assert.True(JToken.DeepEquals(JToken.Parse(expectedStr), JToken.Parse(actual)),
                $"Expected: {expectedStr}, actual: {actual}");
        }

        [Fact]
        public async Task Status_Test()
        {
            var statusPage = await Client.GetAsync("/status");
            var statusContent = await HtmlHelpers.GetHtmlDocumentAsync(statusPage);
            
            Assert.Equal(HttpStatusCode.OK, statusPage.StatusCode);
            Assert.Equal("Healthy", statusContent.DocumentElement.TextContent);
        }

        [Fact]
        public async Task GetSetup_Token_Test()
        {
            var statusPage = await Client.GetAsync("/token?code=1&redirect_uri=2&state=3");
            var statusContent = await HtmlHelpers.GetHtmlDocumentAsync(statusPage);

            Assert.Equal(HttpStatusCode.OK, statusPage.StatusCode);
            var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(statusContent.DocumentElement.TextContent);

            Assert.Equal("mock_github_token", result["access_token"]);
            Assert.Equal("bearer", result["token_type"]);
            Assert.Equal(GitHubClientMock.CurrentUserData.Data.Name, result["user_name"]);
            Assert.Equal(GitHubClientMock.CurrentUserEmailData.Data.FirstOrDefault().Email, result["email"]);
            Assert.Equal(GitHubClientMock.CurrentUserData.Data.AvatarUrl, result["avatar_url"]);
        }


        [Fact]
        public async Task PostSetup_Webhook_ping_Test()
        {
            string requestContent = Tests.Properties.Resources.Github_Webhook_ping;
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, "/webhook");
            req.Headers.Add("X-GitHub-Event", "ping");
            req.Headers.Add("X-GitHub-Delivery", "mock-delivery-id");
            req.Headers.Add("X-Hub-Signature", Tests.SetupController_Tests.GithubSignature("123", Encoding.UTF8.GetBytes(requestContent)));
            req.Content = new StringContent(requestContent, Encoding.UTF8, "application/json");

            var response = await Client.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PostSetup_Webhook_purchased_Test()
        {
            string requestContent = Tests.Properties.Resources.Github_Webhook_purchased;
            HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, "/webhook");
            req.Headers.Add("X-GitHub-Event", "marketplace_purchase");
            req.Headers.Add("X-GitHub-Delivery", "mock-delivery-id");
            req.Headers.Add("X-Hub-Signature", Tests.SetupController_Tests.GithubSignature("123", Encoding.UTF8.GetBytes(requestContent)));
            req.Content = new StringContent(requestContent, Encoding.UTF8, "application/json");

            var response = await Client.SendAsync(req);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            LoggingMock.Verify(e => e.ReportSetupLog(It.Is<ElasticsearchSetupDocument>(m =>
                m.Action == "marketplace_purchase-purchased" && m.ActionId == "mock-delivery-id" && m.ActionOriginator == "sender-username" && m.Subscriber == "account-username"
            )));
        }

        [Fact]
        public async Task GetOrganizations_Test()
        {
            var response = await AuthClient.GetAsync("/api/organization");
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EqualExpected(GitHubClientMock.OrganizationsData.ExpectedResult, content.DocumentElement.TextContent);
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d => 
                d.ControllerName == "Organization" && d.ActionName == "GetOrgs")));
        }

        [Fact]
        public async Task GetRepos_Test()
        {
            var response = await AuthClient.GetAsync("/api/repository/repositories");
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EqualExpected(GitHubClientMock.RepositoriesData.ExpectedResult, content.DocumentElement.TextContent);
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d =>
                d.ControllerName == "Repository" && d.ActionName == "GetRepos")));
        }

        [Fact]
        public async Task GetLabels_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var response = await AuthClient.GetAsync($"/api/repository/{repoId}/labels");
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EqualExpected(GitHubClientMock.LabelsData[repoId].ExpectedResult, content.DocumentElement.TextContent);
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d =>
                d.ControllerName == "Repository" && d.ActionName == "GetLabels")));
        }

        [Fact]
        public async Task GetMilestones_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var response = await AuthClient.GetAsync($"/api/repository/{repoId}/milestones");
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EqualExpected(GitHubClientMock.MilestonesData[repoId].ExpectedResult, content.DocumentElement.TextContent);
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d =>
                d.ControllerName == "Repository" && d.ActionName == "GetMilestones")));
        }

        [Fact]
        public async Task GetCollaborators_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var response = await AuthClient.GetAsync($"/api/repository/{repoId}/collaborators");
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EqualExpected(GitHubClientMock.CollaboratorsData[repoId].ExpectedResult, content.DocumentElement.TextContent);
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d =>
                d.ControllerName == "Repository" && d.ActionName == "GetCollaborators")));
        }

        [Fact]
        public async Task GetIssuesSingleRepo_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var response = await AuthClient.GetAsync($"/api/repository/{repoId}/issues");
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EqualExpected(GitHubClientMock.RepositoryIssuesData[repoId].ExpectedResult, content.DocumentElement.TextContent);
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d =>
                d.ControllerName == "Repository" && d.ActionName == "GetIssuesRepo")));
        }

        [Fact]
        public async Task GetIssuesCurrentUser_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var response = await AuthClient.GetAsync($"/api/repository/issues");
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EqualExpected(GitHubClientMock.CurrentUserIssuesData.ExpectedResult, content.DocumentElement.TextContent);
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d =>
                d.ControllerName == "Repository" && d.ActionName == "GetIssuesRepos")));
        }

        [Fact]
        public async Task GetIssuesMultiRepo1_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var response = await AuthClient.GetAsync($"/api/repository/issues?repository_ids={repoId}");
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            EqualExpected(GitHubClientMock.RepositoryIssuesData[repoId].ExpectedResult, content.DocumentElement.TextContent);
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d =>
                d.ControllerName == "Repository" && d.ActionName == "GetIssuesRepos")));
        }

        [Fact]
        public async Task GetIssuesMultiRepo2_Test()
        {
            long repo1Id = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            long repo2Id = GitHubClientMock.RepositoriesData.Data.Skip(1).FirstOrDefault().Id;
            var response = await AuthClient.GetAsync($"/api/repository/issues?repository_ids={repo1Id}&repository_ids={repo2Id}");
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // merge expected results
            JArray expected = JArray.FromObject(GitHubClientMock.RepositoryIssuesData[repo1Id].ExpectedResult);
            expected.Merge(JArray.FromObject(GitHubClientMock.RepositoryIssuesData[repo2Id].ExpectedResult), new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Union});

            EqualExpected(expected.ToObject<dynamic>(), content.DocumentElement.TextContent);
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d =>
                d.ControllerName == "Repository" && d.ActionName == "GetIssuesRepos")));
        }

        [Fact]
        public async Task PostExport_Test()
        {
            var issuesToExport = GitHubClientMock.RepositoryIssuesData.Select((kv) => new { repositoryid = kv.Key, issueno = kv.Value.Data.FirstOrDefault().Number});

            var response = await AuthClient.PostAsync("/api/export/export"
                , new StringContent(JsonConvert.SerializeObject(issuesToExport), Encoding.UTF8, "application/json"));
            var content = await HtmlHelpers.GetHtmlDocumentAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            JObject result = JObject.Parse(content.DocumentElement.TextContent);
            Assert.Matches($"{AuthClient.BaseAddress}api/Export/download/[-a-zA-Z0-9]*", result["downloadlink"].Value<string>());
            Assert.Matches("[-a-zA-Z0-9]*", result["id"].Value<string>());
            LoggingMock.Verify(e => e.ReportAccessLog(It.Is<ElasticsearchAccessLogDocument>(d =>
                d.ControllerName == "Export" && d.ActionName == "PostExport")));
            Output.WriteLine($"{LoggingMock.Invocations.Count}");
        }

        [Fact]
        public async Task GetDownload_Test()
        {
            var response = await AuthClient.GetAsync("/api/export/download/123-Mock-321");

            var content = await response.Content.ReadAsByteArrayAsync();
            
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            PdfApiMock.Verify(e =>e.DownloadFileAsync("mockroot/123-Mock-321.pdf", It.IsAny<string>(), It.IsAny<string>()));
            Assert.Equal("file 123-Mock-321.pdf content", Encoding.UTF8.GetString(content));
            Assert.Matches("Issues-.{4}-.{2}-.{2}.pdf", response.Content.Headers.ContentDisposition.FileNameStar);
        }
        
    }
}
