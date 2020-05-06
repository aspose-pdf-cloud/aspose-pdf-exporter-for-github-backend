using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.AutoMoq;
using AutoFixture.Kernel;
using Moq;
using Octokit;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks
{
    /// <summary>
    /// Contains Mocks for Octokit library.
    /// Not all clients/functions are mocked, only those we need
    /// we havily use AutoFixture here
    /// </summary>
    public class GitHubClientMock
    {
        public class MockData<T>
        {
            public T Data;
            public dynamic ExpectedResult;

            public MockData(T data, Func<T, dynamic> expectedResult = null)
            {
                Data = data;
                if (null != expectedResult)
                    ExpectedResult = expectedResult(Data);
            }
        }

        public Mock<IGitHubClient> MockGitHubClient;
        public Mock<IOrganizationsClient> MockOrganizationCli;

        internal Mock<IRepositoriesClient> _mockRepositoriesCli;
        public Mock<IRepositoriesClient> MockRepositoriesCli
        {
            get
            {
                if (null == _mockRepositoriesCli)
                    _mockRepositoriesCli = new Mock<IRepositoriesClient>();
                return _mockRepositoriesCli;
            }
        }
        internal Mock<IIssuesClient> _mockIssueCli;
        public Mock<IIssuesClient> MockIssueCli
        {
            get
            {
                if (null == _mockIssueCli)
                    _mockIssueCli = new Mock<IIssuesClient>();
                return _mockIssueCli;
            }
        }
        public Mock<IIssuesLabelsClient> MockLabelsCli;
        public Mock<IMilestonesClient> MockMilestonesCli;
        public Mock<IRepoCollaboratorsClient> MockRepoCollaboratorsCli;
        public Mock<IIssueCommentsClient> MockIssueCommentsCli;
        public Mock<IUsersClient> MockUsersCli;
        public Mock<IUserEmailsClient> MockUserEmailCli;


        public MockData<IReadOnlyList<Organization>> OrganizationsData;
        public MockData<IReadOnlyList<Repository>> RepositoriesData;
        public Dictionary<long, MockData<IReadOnlyList<Label>>> LabelsData;
        public Dictionary<long, MockData<IReadOnlyList<Milestone>>> MilestonesData;
        public Dictionary<long, MockData<IReadOnlyList<User>>> CollaboratorsData;

        public MockData<IReadOnlyList<Issue>> CurrentUserIssuesData;
        public Dictionary<long, MockData<IReadOnlyList<Issue>>> RepositoryIssuesData;
        public Dictionary<string, MockData<IReadOnlyList<IssueComment>>> IssueCommentsData;
        public MockData<User> CurrentUserData;
        public MockData<IReadOnlyList<EmailAddress>> CurrentUserEmailData;

        /// <summary>
        /// create simple fixture using greedy constructor
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public IFixture CreateSimpleFixture<T>()
        {
            return new Fixture().Customize(new ConstructorCustomization(typeof(T),
                    new GreedyConstructorQuery()))
                .Customize(new AutoMoqCustomization());
        }

        /// <summary>
        /// clear all mock invocations. Clients should use it if GitHubClientMock being used in IClassFixture
        /// i.e. created only once for text context
        /// </summary>
        public void ClearInvocations()
        {
            MockGitHubClient.Invocations.Clear();
            MockOrganizationCli.Invocations.Clear();
            MockRepositoriesCli.Invocations.Clear();
            MockIssueCli.Invocations.Clear();
            MockLabelsCli.Invocations.Clear();
            MockMilestonesCli.Invocations.Clear();
            MockRepoCollaboratorsCli.Invocations.Clear();
            MockIssueCommentsCli.Invocations.Clear();
            MockUsersCli.Invocations.Clear();
            MockUserEmailCli.Invocations.Clear();
        }
        /// <summary>
        /// Creates constructor query for AutoFixture ingoring parameters specified by ignoreParamNames
        /// </summary>
        public class RestrictedGreedyConstructorQuery : IMethodQuery
        {
            private string[] ignoredParams;
            public RestrictedGreedyConstructorQuery(params string[] ignoreParamNames)
            {
                ignoredParams = ignoreParamNames;
            }
            public IEnumerable<IMethod> SelectMethods(Type type)
            {
                if (type == null) throw new ArgumentNullException(nameof(type));
                var result = from ci in type.GetTypeInfo().GetConstructors()
                    let parameters = ci.GetParameters()
                    where ignoredParams == null || ignoredParams.Length == 0 || parameters.All(p => (p.ParameterType == type && ignoredParams.Contains(p.Name)) || p.ParameterType != type)
                    orderby parameters.Length descending
                    select new ConstructorMethod(ci) as IMethod;
                return result;
            }
        }

        /// <summary>
        /// Build GitHubClient and mock specific clients based on parameters
        /// </summary>
        /// <param name="mockAll"></param>
        /// <param name="mockOrganizations"></param>
        /// <param name="mockRepositories"></param>
        /// <param name="mockLabels"></param>
        /// <param name="mockMilestones"></param>
        /// <param name="mockCollaborators"></param>
        /// <param name="mockIssues4CurrentUser"></param>
        /// <param name="mockRepositoryIssues"></param>
        /// <param name="mockIssueData"></param>
        /// <param name="mockComments"></param>
        /// <param name="mockUser"></param>
        /// <param name="mockRepo"></param>
        public GitHubClientMock(bool mockAll = false, bool mockOrganizations = false, bool mockRepositories = false
            , bool mockLabels = false, bool mockMilestones = false
            , bool mockCollaborators = false, bool mockIssues4CurrentUser = false
            , bool mockRepositoryIssues = false, bool mockIssueData = false
            , bool mockComments = false, bool mockUser = false
            , bool mockRepo = false)
        {
            MockGitHubClient = new Mock<IGitHubClient>();
            MockOrganizationCli = new Mock<IOrganizationsClient>();
            //MockRepositoriesCli = new Mock<IRepositoriesClient>();
            MockLabelsCli = new Mock<IIssuesLabelsClient>();
            //MockIssueCli = new Mock<IIssuesClient>();

            MockMilestonesCli = new Mock<IMilestonesClient>();
            MockRepoCollaboratorsCli = new Mock<IRepoCollaboratorsClient>();
            MockIssueCommentsCli = new Mock<IIssueCommentsClient>();

            MockUserEmailCli = new Mock<IUserEmailsClient>();
            MockUsersCli = new Mock<IUsersClient>();
            var random = new Random();
            if (mockOrganizations || mockAll)
            {
                var fixture = CreateSimpleFixture<Organization>();
                OrganizationsData = new MockData<IReadOnlyList<Organization>>(
                    fixture.CreateMany<Organization>(random.Next(1, 5)).ToList()
                    , data => data.Select(o => new
                    {
                        id = o.Id,
                        name = o.Login,
                        avatar_url = o.AvatarUrl
                    }).ToList<dynamic>());

                MockOrganizationCli.Setup(c => c.GetAllForCurrent())
                    .Returns(Task.FromResult(OrganizationsData.Data));
                MockOrganizationCli.Setup(c => c.GetAllForCurrent(It.IsAny<ApiOptions>()))
                    .Returns(Task.FromResult(OrganizationsData.Data));

                MockGitHubClient.Setup(c => c.Organization).Returns(MockOrganizationCli.Object);
            }

            if (mockRepositories || mockAll)
            {
                var fixture = new Fixture()
                        // do not initialize parent and source constructor parameters
                        .Customize(new ConstructorArgumentRelay<Repository, Repository>("parent", null).ToCustomization())
                        .Customize(new ConstructorArgumentRelay<Repository, Repository>("source", null).ToCustomization())
                        .Customize(new ConstructorCustomization(typeof(Repository),
                            new RestrictedGreedyConstructorQuery()))
                        .Customize(new ConstructorCustomization(typeof(RepositoryPermissions),
                            new GreedyConstructorQuery()))
                        .Customize(new ConstructorCustomization(typeof(LicenseMetadata),
                            new GreedyConstructorQuery()))
                        .Customize(new ConstructorCustomization(typeof(User),
                            new GreedyConstructorQuery()))
                        .Customize(new AutoMoqCustomization())
                    ;

                RepositoriesData = new MockData<IReadOnlyList<Repository>>(
                    fixture.CreateMany<Repository>(random.Next(2, 5)).ToList()
                    , data => data.Select(r => new
                    {
                        name = r.FullName,
                        description = r.Description,
                        updated = r.UpdatedAt,
                        id = r.Id
                    }).ToList<dynamic>());
                MockRepositoriesCli.Setup(c => c.GetAllForCurrent())
                    .Returns(Task.FromResult(RepositoriesData.Data));
                MockRepositoriesCli.Setup(c => c.GetAllForCurrent(It.IsAny<ApiOptions>()))
                    .Returns(Task.FromResult(RepositoriesData.Data));
                
                // setup IRepositoriesClient::Get() for each created repository
                foreach(var r in RepositoriesData.Data)
                    MockRepositoriesCli.Setup(c => c.Get(r.Id))
                        .Returns(Task.FromResult(r));

            }

            if (mockLabels || mockAll)
            {
                var fixture = CreateSimpleFixture<Label>();
                LabelsData = new Dictionary<long, MockData<IReadOnlyList<Label>>>();
                
                // setup labels for each repository
                foreach (var r in RepositoriesData.Data)
                {
                    var labels = new MockData<IReadOnlyList<Label>>(
                        fixture.CreateMany<Label>(random.Next(1, 5)).ToList()
                        , data => data.Select(l => new
                        {
                            id = l.Id,
                            name = l.Name,
                            description = l.Description
                        }).ToList<dynamic>());
                    LabelsData.Add(r.Id, labels);

                    MockLabelsCli.Setup(c => c.GetAllForRepository(r.Id))
                        .Returns((long repoId) => Task.FromResult(LabelsData[repoId].Data));
                    MockLabelsCli.Setup(c => c.GetAllForRepository(r.Id, It.IsAny<ApiOptions>()))
                        .Returns((long repoId, ApiOptions o) => Task.FromResult(LabelsData[repoId].Data));
                }

                MockIssueCli.Setup(e => e.Labels).Returns(MockLabelsCli.Object);
            }

            if (mockMilestones || mockAll)
            {
                var fixture = CreateSimpleFixture<Milestone>()
                    .Customize(new ConstructorCustomization(typeof(User), new GreedyConstructorQuery()));
                MilestonesData = new Dictionary<long, MockData<IReadOnlyList<Milestone>>>();
                // setup milestones for each repository
                foreach (var r in RepositoriesData.Data)
                {
                    var milestones = new MockData<IReadOnlyList<Milestone>>(
                        fixture.CreateMany<Milestone>(random.Next(1, 5)).ToList()
                        , data => data.Select(m => new
                        {
                            id = m.Id,
                            number = m.Number,
                            title = m.Title,
                            created = m.CreatedAt,
                            description = m.Description,
                            state = m.State.StringValue
                        }).ToList<dynamic>());
                    MilestonesData.Add(r.Id, milestones);
                    MockMilestonesCli.Setup(c => c.GetAllForRepository(r.Id))
                        .Returns((long repoId) => Task.FromResult(MilestonesData[repoId].Data));
                    MockMilestonesCli.Setup(c => c.GetAllForRepository(r.Id, It.IsAny<ApiOptions>()))
                        .Returns((long repoId, ApiOptions o) => Task.FromResult(MilestonesData[repoId].Data));
                }

                MockIssueCli.Setup(e => e.Milestone).Returns(MockMilestonesCli.Object);
            }

            if (mockCollaborators || mockAll)
            {
                var fixture = CreateSimpleFixture<User>();
                CollaboratorsData = new Dictionary<long, MockData<IReadOnlyList<User>>>();
                // setup collaborators for each repository
                foreach (var r in RepositoriesData.Data)
                {
                    var collaborators = new MockData<IReadOnlyList<User>>(
                        fixture.CreateMany<User>(random.Next(1, 5)).ToList()
                        , data => data.Select(c => new
                        {
                            id = c.Id,
                            login = c.Login,
                            avatar_url = c.AvatarUrl,
                            url = c.Url,
                        }).ToList<dynamic>());
                    CollaboratorsData.Add(r.Id, collaborators);
                    MockRepoCollaboratorsCli.Setup(c => c.GetAll(It.IsAny<long>()))
                        .Returns((long repoId) => Task.FromResult(CollaboratorsData[repoId].Data));
                    MockRepoCollaboratorsCli.Setup(c => c.GetAll(It.IsAny<long>(), It.IsAny<ApiOptions>()))
                        .Returns((long repoId, ApiOptions o) => Task.FromResult(CollaboratorsData[repoId].Data));
                }

                MockRepositoriesCli.Setup(e => e.Collaborator).Returns(MockRepoCollaboratorsCli.Object);
            }

            if (mockIssues4CurrentUser || mockAll)
            {
                var fixture = CreateSimpleFixture<Issue>()
                    .Customize(new ConstructorCustomization(typeof(User),
                        new GreedyConstructorQuery()))
                    .Customize(new ConstructorCustomization(typeof(Label),
                        new GreedyConstructorQuery()))
                    .Customize(new ConstructorCustomization(typeof(Milestone),
                        new GreedyConstructorQuery()))
                    .Customize(new ConstructorCustomization(typeof(Reaction),
                        new GreedyConstructorQuery()))
                    .Customize(new CompositeCustomization(
                        new ConstructorCustomization(typeof(Repository),new RestrictedGreedyConstructorQuery())
                        , new ConstructorArgumentRelay<Repository, Repository>("parent", null).ToCustomization()
                        , new ConstructorArgumentRelay<Repository, Repository>("source", null).ToCustomization()
                        ))
                    ;
                CurrentUserIssuesData = new MockData<IReadOnlyList<Issue>>(
                    fixture.CreateMany<Issue>(random.Next(1, 5)).ToList()
                    , data => data.Select(i => new
                    {
                        id = i.Id,
                        number = i.Number,
                        state = i.State.StringValue,
                        title = i.Title,
                        assignee = i.Assignee?.Name,
                        repo_id = i?.Repository?.Id
                    }).ToList<dynamic>());
                MockIssueCli.Setup(c => c.GetAllForCurrent(It.IsAny<IssueRequest>(), It.IsAny<ApiOptions>()))
                    .Returns(Task.FromResult(CurrentUserIssuesData.Data));
            }

            if (mockIssueData || mockRepositoryIssues || mockAll)
            {
                var fixture = CreateSimpleFixture<Issue>()
                    .Customize(new ConstructorCustomization(typeof(User),
                        new GreedyConstructorQuery()))
                    .Customize(new ConstructorCustomization(typeof(Label),
                        new GreedyConstructorQuery()))
                    .Customize(new ConstructorCustomization(typeof(Milestone),
                        new GreedyConstructorQuery()))
                    .Customize(new ConstructorCustomization(typeof(Reaction),
                        new GreedyConstructorQuery()))
                    .Customize(new ConstructorCustomization(typeof(ReactionSummary),
                        new GreedyConstructorQuery()))
                    .Customize(new ConstructorArgumentRelay<Issue, Repository>("repository", null).ToCustomization())
                    .Customize(new ConstructorArgumentRelay<Issue, int>("comments", mockComments || mockAll ? random.Next(0, 10) : 0).ToCustomization())
                    //.Customize(new ConstructorCustomization(typeof(Repository),
                    //    new GreedyConstructorQuery()))
                    ;
                RepositoryIssuesData = new Dictionary<long, MockData<IReadOnlyList<Issue>>>();
                // setup issues for each repository
                foreach (var r in RepositoriesData.Data)
                {
                    var repoIssueData = new MockData<IReadOnlyList<Issue>>(
                        fixture.CreateMany<Issue>(random.Next(1, 5)).ToList()
                        , data => data.Select(i => new
                        {
                            id = i.Id,
                            number = i.Number,
                            state = i.State.StringValue,
                            title = i.Title,
                            assignee = i.Assignee?.Name,
                            repo_id = r.Id
                        }).ToList<dynamic>());
                    RepositoryIssuesData.Add(r.Id, repoIssueData);
                    if (mockIssueData || mockAll)
                        foreach (var i in repoIssueData.Data) // setup IIssuesClient.Get() for each issue whithin each repository
                            MockIssueCli.Setup(c => c.Get(r.Id, i.Number))
                                .Returns((long repoId, int number) => Task.FromResult(RepositoryIssuesData[repoId].Data.FirstOrDefault(ii => ii.Number == number)));

                    if (mockRepositoryIssues || mockAll) // setup IIssuesClient.GetAllForRepository() for each repository
                        MockIssueCli.Setup(c =>
                                c.GetAllForRepository(r.Id, It.IsAny<RepositoryIssueRequest>(), It.IsAny<ApiOptions>()))
                            .Returns((long repoId, RepositoryIssueRequest r, ApiOptions o) => Task.FromResult(RepositoryIssuesData[repoId].Data));
                }
            }

            if (mockComments || mockAll)
            {
                var fixture = CreateSimpleFixture<IssueComment>()
                    .Customize(new ConstructorCustomization(typeof(User), new GreedyConstructorQuery()))
                    .Customize(new ConstructorCustomization(typeof(ReactionSummary), new GreedyConstructorQuery()))
                    ;
                
                
                IssueCommentsData = new Dictionary<string, MockData<IReadOnlyList<IssueComment>>>();
                
                // setup comments for each issue in each repository
                foreach (var r in RepositoryIssuesData)
                    foreach (var i in r.Value.Data)
                    {
                        var comments = new MockData<IReadOnlyList<IssueComment>>(
                            fixture.CreateMany<IssueComment>(i.Comments).ToList()
                            , data => data.ToList<dynamic>());

                        IssueCommentsData.Add($"{r.Key}-{i.Number}", comments);


                        MockIssueCommentsCli.Setup(c => c.GetAllForIssue(r.Key, i.Number))
                            .Returns((long repoId, int issueNo) => Task.FromResult(IssueCommentsData[$"{repoId}-{issueNo}"].Data));
                        MockIssueCommentsCli.Setup(c =>
                                c.GetAllForIssue(r.Key, i.Number, It.IsAny<ApiOptions>()))
                            .Returns((long repoId, int issueNo, ApiOptions o) => Task.FromResult(IssueCommentsData[$"{repoId}-{issueNo}"].Data));
                    }

                MockIssueCli.Setup(e => e.Comment).Returns(MockIssueCommentsCli.Object);
            }

            if (mockUser || mockAll) 
            {
                // setup current user
                var fixtureUser = CreateSimpleFixture<User>()
                    .Customize(new ConstructorCustomization(typeof(Plan), new GreedyConstructorQuery()));
                CurrentUserData = new MockData<User>(fixtureUser.Create<User>());

                var fixtureEmail = CreateSimpleFixture<EmailAddress>()
                    .Customize(new ConstructorArgumentRelay<EmailAddress, bool>("primary", true).ToCustomization());
                CurrentUserEmailData = new MockData<IReadOnlyList<EmailAddress>>(fixtureEmail.CreateMany<EmailAddress>(random.Next(1, 5)).ToList());

                MockUserEmailCli.Setup(e => e.GetAll()).ReturnsAsync(CurrentUserEmailData.Data);
                
                MockUsersCli.Setup(c => c.Current()).ReturnsAsync(CurrentUserData.Data);
                MockUsersCli.Setup(e => e.Email).Returns(MockUserEmailCli.Object);
                MockGitHubClient.Setup(c => c.User).Returns(MockUsersCli.Object);
            }

            if (mockRepo || mockAll)
            {
                // setup IRepositoriesClient.Get() for each repository
                foreach (var r in RepositoriesData.Data)
                {
                    MockRepositoriesCli.Setup(c => c.Get(r.Id))
                        .Returns((long repoId) => Task.FromResult(RepositoriesData.Data.FirstOrDefault(rr => rr.Id == repoId)));
                }
            }

            var mockConnection = new Mock<IConnection>();
            MockGitHubClient.Setup(c => c.Connection).Returns(mockConnection.Object);
            if (null != _mockIssueCli)
                MockGitHubClient.Setup(c => c.Issue).Returns(MockIssueCli.Object);
            if (null != _mockRepositoriesCli)
                MockGitHubClient.Setup(c => c.Repository).Returns(MockRepositoriesCli.Object);
        }
    }
}
