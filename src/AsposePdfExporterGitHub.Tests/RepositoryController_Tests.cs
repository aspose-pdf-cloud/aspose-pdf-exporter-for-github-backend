using System;
using System.Collections;
using Xunit;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Controllers;
using AutoFixture;
using Octokit;
using Xunit.Abstractions;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests
{
    public class RepositoryControllerFixture : ControllerFixture<RepositoryController>
    {
        public override void Initialize()
        {
            GitHubClientMock = new GitHubClientMock(mockRepositories: true, mockLabels: true
                , mockMilestones: true, mockCollaborators: true, mockIssues4CurrentUser: true
                , mockRepositoryIssues: true
            );
            ClientServiceMock = new GithubExporterClientServiceMock(GitHubClientMock);
        }
    }

    [Trait("Controllers", "OrganizationController")]
    public class RepositoryController_Tests : IClassFixture<RepositoryControllerFixture>
    {
        internal static void EqualExpected(dynamic expected, IEnumerable<dynamic> actual) =>
            OrganizationController_Tests.EqualExpected(expected, actual);

        internal RepositoryControllerFixture Fixture;
        internal RepositoryController Controller => Fixture.Controller;
        internal GitHubClientMock GitHubClientMock => Fixture.GitHubClientMock;
        internal GithubExporterClientServiceMock ClientServiceMock => Fixture.ClientServiceMock;
        private readonly ITestOutputHelper Output;
        public RepositoryController_Tests(RepositoryControllerFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            Fixture.ClearInvocations();
            Output = output;
        }

        public class IssueRequestTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                foreach (string filter in new[] {null, "assigned", "created", "mentioned", "subscribed", "all" })
                    foreach (string state in new[] { null, "open", "closed", "all" })
                        foreach (string sort in new[] { null, "created", "updated", "comments" })
                            foreach (string direction in new[] {null, "asc", "desc"})
                            {
                                Random r = new Random(DateTime.Now.Second);
                                var fixture = new Fixture();
                                List<string> labels = r.Next(0, 10) < 5 ? null : fixture.CreateMany<string>(r.Next(1, 3)).ToList();
                                DateTime? since = r.Next(0, 10) < 5 ? null : (DateTime?)fixture.Create<DateTime>();
                                yield return new object[] {filter, state, labels?.ToArray(), sort, direction, since};
                            }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(IssueRequestTestData))]
        public void IssueRequest_Test(string filter, string state, string[] labels, string sort, string direction, DateTime? since)
        {
            var req = new IssueRequest();
            Controller.IssueRequest(req, filter, state, labels, sort, direction, since);
            switch (filter)
            {
                case null:
                    break;

                case "assigned": Assert.Equal(IssueFilter.Assigned, req.Filter);
                    break;
                case "created":
                    Assert.Equal(IssueFilter.Created, req.Filter);
                    break;
                case "mentioned":
                    Assert.Equal(IssueFilter.Mentioned, req.Filter);
                    break;
                case "subscribed":
                    Assert.Equal(IssueFilter.Subscribed, req.Filter);
                    break;
                case "all":
                    Assert.Equal(IssueFilter.All, req.Filter);
                    break;
            }

            switch (state)
            {
                case null:
                    break;

                case "open":
                    Assert.Equal(ItemStateFilter.Open, req.State);
                    break;
                case "closed":
                    Assert.Equal(ItemStateFilter.Closed, req.State);
                    break;
                case "all":
                    Assert.Equal(ItemStateFilter.All, req.State);
                    break;
            }

            if (labels == null)
                Assert.Empty(req.Labels);
            else
                Assert.True(labels.SequenceEqual(req.Labels), "bales do not match");

            switch (sort)
            {
                case null:
                    break;

                case "created":
                    Assert.Equal(IssueSort.Created, req.SortProperty);
                    break;
                case "updated":
                    Assert.Equal(IssueSort.Updated, req.SortProperty);
                    break;
                case "comments":
                    Assert.Equal(IssueSort.Comments, req.SortProperty);
                    break;
            }

            switch (direction)
            {
                case null:
                    break;

                case "asc":
                    Assert.Equal(SortDirection.Ascending, req.SortDirection);
                    break;
                case "desc":
                    Assert.Equal(SortDirection.Descending, req.SortDirection);
                    break;
            }

            Assert.Equal(since, req.Since);
        }

        [Fact]
        public void RepositoryIssueRequest_Test()
        {
            var req = new RepositoryIssueRequest();
            Controller.RepositoryIssueRequest(req, "1", "2 ", null, " 4");
            Assert.Equal("1", req.Milestone);
            Assert.Equal("2", req.Assignee);
            Assert.Null(req.Creator);
            Assert.Equal("4", req.Mentioned);
        }

        public class GetIssuesTestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {

                foreach (var p in new IssueRequestTestData())
                {
                    yield return new object[] { new List<long>{ 239493576 } , p[..], "milestone", "assignee", "creator", "mentioned" };
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }


        //[Theory]
        //[ClassData(typeof(GetIssuesTestData))]
        [Fact]
        public async Task GetIssues4CurrentUser_Test(/*IEnumerable<long?> repository_ids, string filter, string state, string[] labels, string sort, string direction, DateTime? since
            , string milestone, string assignee, string creator, string mentioned*/)
        {
            var result = await Controller.GetIssues(null);
            Assert.Null(result.PageNo);
            EqualExpected(GitHubClientMock.CurrentUserIssuesData.ExpectedResult, result.Result);
            GitHubClientMock.MockIssueCli.Verify(e => e.GetAllForCurrent(It.IsAny<IssueRequest>(), It.IsAny<ApiOptions>()));
        }

        [Fact]
        public async Task GetIssues4Repo_Test()
        {
            var repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var result = await Controller.GetIssues(new long?[] {repoId});
            Assert.Null(result.PageNo);
            EqualExpected(GitHubClientMock.RepositoryIssuesData[repoId].ExpectedResult, result.Result);
            GitHubClientMock.MockIssueCli.Verify(e => e.GetAllForRepository(repoId, It.IsAny<RepositoryIssueRequest>(), It.IsAny<ApiOptions>()));
        }

        [Fact]
        public async Task GetReposNoPage_Test()
        {
            var result = await Controller.GetRepos();
            Assert.Null(result.PageNo);
            EqualExpected(GitHubClientMock.RepositoriesData.ExpectedResult, result.Result);
            GitHubClientMock.MockRepositoriesCli.Verify(e => e.GetAllForCurrent(It.IsAny<ApiOptions>()));
        }
        [Fact]
        public async Task GetRepos1Page_Test()
        {
            var result = await Controller.GetRepos(pageNo: 1);
            Assert.Equal(1, result.PageNo);
            EqualExpected(GitHubClientMock.RepositoriesData.ExpectedResult, result.Result);
            GitHubClientMock.MockRepositoriesCli.Verify(e => e.GetAllForCurrent(It.Is<ApiOptions>(m => m.StartPage == 1)));
        }

        [Fact]
        public async Task GetLabelsNoPage_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var result = await Controller.GetLabels(repoId);
            Assert.Null(result.PageNo);
            EqualExpected(GitHubClientMock.LabelsData[repoId].ExpectedResult, result.Result);
            GitHubClientMock.MockLabelsCli.Verify(e => e.GetAllForRepository(repoId, It.IsAny<ApiOptions>()));
        }

        [Fact]
        public async Task GetLabels1Page_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var result = await Controller.GetLabels(repoId, pageNo:1);
            Assert.Equal(1, result.PageNo);
            EqualExpected(GitHubClientMock.LabelsData[repoId].ExpectedResult, result.Result);
            GitHubClientMock.MockLabelsCli.Verify(e => e.GetAllForRepository(repoId, It.Is<ApiOptions>(m => m.StartPage == 1)));
        }

        [Fact]
        public async Task GetMilestonesNoPage_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var result = await Controller.GetMilestones(repoId);
            Assert.Null(result.PageNo);
            EqualExpected(GitHubClientMock.MilestonesData[repoId].ExpectedResult, result.Result);
            GitHubClientMock.MockMilestonesCli.Verify(e => e.GetAllForRepository(repoId, It.IsAny<ApiOptions>()));
        }

        [Fact]
        public async Task GetMilestones1Page_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var result = await Controller.GetMilestones(repoId, pageNo: 1);
            Assert.Equal(1, result.PageNo);
            EqualExpected(GitHubClientMock.MilestonesData[repoId].ExpectedResult, result.Result);
            GitHubClientMock.MockMilestonesCli.Verify(e => e.GetAllForRepository(repoId, It.Is<ApiOptions>(m => m.StartPage == 1)));
        }

        [Fact]
        public async Task GetCollaboratorsNoPage_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var result = await Controller.GetCollaborators(repoId);
            Assert.Null(result.PageNo);
            EqualExpected(GitHubClientMock.CollaboratorsData[repoId].ExpectedResult, result.Result);
            GitHubClientMock.MockRepoCollaboratorsCli.Verify(e => e.GetAll(repoId, It.IsAny<ApiOptions>()));
        }

        [Fact]
        public async Task GetCollaborators1Page_Test()
        {
            long repoId = GitHubClientMock.RepositoriesData.Data.FirstOrDefault().Id;
            var result = await Controller.GetCollaborators(repoId, pageNo: 1);
            Assert.Equal(1, result.PageNo);
            EqualExpected(GitHubClientMock.CollaboratorsData[repoId].ExpectedResult, result.Result);
            GitHubClientMock.MockRepoCollaboratorsCli.Verify(e => e.GetAll(repoId, It.Is<ApiOptions>(m => m.StartPage == 1)));
        }
    }
}
