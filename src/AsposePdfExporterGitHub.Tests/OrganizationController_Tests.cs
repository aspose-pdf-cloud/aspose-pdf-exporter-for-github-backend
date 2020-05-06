using Xunit;
using Moq;
using System.Collections.Generic;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Controllers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using Xunit.Abstractions;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests
{
    public class OrganizationControllerFixture : ControllerFixture<OrganizationController>
    {
        public override void Initialize()
        {
            GitHubClientMock = new GitHubClientMock(mockOrganizations: true);
            ClientServiceMock = new GithubExporterClientServiceMock(GitHubClientMock);
        }
    }

    [Trait("Controllers", "OrganizationController")]
    public class OrganizationController_Tests : IClassFixture<OrganizationControllerFixture>
    {
        private OrganizationControllerFixture Fixture;
        internal OrganizationController Controller => Fixture.Controller;
        internal GitHubClientMock GitHubClientMock => Fixture.GitHubClientMock;
        internal readonly ITestOutputHelper Output;
        public OrganizationController_Tests(OrganizationControllerFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            Fixture.ClearInvocations();
            Output = output;
        }

        internal static void EqualExpected(dynamic expected, IEnumerable<dynamic> actual)
        {
            var expectedStr = JsonConvert.SerializeObject(expected);
            var actualStr = JsonConvert.SerializeObject(actual);
            Assert.True(JToken.DeepEquals(JToken.Parse(expectedStr), JToken.Parse(actualStr)), 
                $"Expected: {expectedStr}, actual: {actualStr}");
        }

        [Fact]
        public async void GetOrgsNoPage_Test()
        {
            var result = await Controller.GetOrgs();
            Assert.NotNull(result);
            Assert.Null(result.PageNo);
            GitHubClientMock.MockOrganizationCli.Verify(e => e.GetAllForCurrent(It.IsAny<ApiOptions>()), Times.Once);
            EqualExpected(GitHubClientMock.OrganizationsData.ExpectedResult, result.Result);
        }
        [Fact]
        public async void GetOrgs1Page_Test()
        {
            var result = await Controller.GetOrgs(pageNo:1);
            Assert.NotNull(result);
            Assert.Equal(1, result.PageNo);
            GitHubClientMock.MockOrganizationCli.Verify(e => e.GetAllForCurrent(It.IsAny<ApiOptions>()), Times.Once);
            EqualExpected(GitHubClientMock.OrganizationsData.ExpectedResult, result.Result);
            Output.WriteLine($"{GitHubClientMock.MockGitHubClient.Invocations.Count}");
        }
    }
}
