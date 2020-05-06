using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octokit;

//https://docs.microsoft.com/en-us/aspnet/core/mvc/models/model-binding?view=aspnetcore-3.1


namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Controllers
{
    /// <summary>
    /// Github repository functions
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class RepositoryController : ControllerBase
    {
        private readonly Services.IAppGithubExporterCli _client;
        
        private IGitHubClient _githubCli => _client?.GitHubClient;
        public RepositoryController(Services.IAppGithubExporterCli client)
        {
            _client = client;
        }

        [HttpGet("repositories", Name = "GetRepos")]
        [Authorize]
        public async Task<Model.ResultPage> GetRepos([FromQuery]int? pageSize = null,
            [FromQuery]int? pageNo = null)
        {
            RepositoryRequest req = new RepositoryRequest();
            var repos = await _githubCli.Repository.GetAllForCurrent(Utils.ApiOptions(pageSize, pageNo));
            //Utils.Save2File("01_repo_getrepos.json", repos);
            return Utils.ToResult(repos.Select(r => new
            {
                name = r.FullName,
                description = r.Description,
                updated = r.UpdatedAt,
                id = r.Id
            }), pageNo);
        }

        [HttpGet("{repository_id}/labels", Name = "GetLabels")]
        [Authorize]
        public async Task<Model.ResultPage> GetLabels([FromRoute]long repository_id,
            [FromQuery]int? pageSize = null,
            [FromQuery]int? pageNo = null)
        {
            
            var labels = await _githubCli.Issue.Labels.GetAllForRepository(repository_id, Utils.ApiOptions(pageSize, pageNo));
            //Utils.Save2File("02_repo_getlabels_239493576.json", labels);
            return Utils.ToResult(labels.Select(l => new
            {
                id = l.Id,
                name = l.Name,
                description = l.Description
            }), pageNo);
        }

        [HttpGet("{repository_id}/milestones", Name = "GetMilestones")]
        [Authorize]
        public async Task<Model.ResultPage> GetMilestones([FromRoute]long repository_id,
            [FromQuery]int? pageSize = null,
            [FromQuery]int? pageNo = null)
        {
            var milestones = await _githubCli.Issue.Milestone.GetAllForRepository(repository_id, Utils.ApiOptions(pageSize, pageNo));
            //Utils.Save2File("03_repo_getmilestones_239493576.json", milestones);
            return Utils.ToResult(milestones.Select(m => new
            {
                id = m.Id,
                number = m.Number,
                title = m.Title,
                created = m.CreatedAt,
                description = m.Description,
                state = m.State.StringValue
            }), pageNo);
        }

        [HttpGet("{repository_id}/collaborators", Name = "GetCollaborators")]
        [Authorize]
        public async Task<Model.ResultPage> GetCollaborators([FromRoute]long repository_id,
            [FromQuery]int? pageSize = null,
            [FromQuery]int? pageNo = null)
        {
            var collaborators = await _githubCli.Repository.Collaborator.GetAll(repository_id, Utils.ApiOptions(pageSize, pageNo));
            //Utils.Save2File("04_repo_collaborators_239493576.json", collaborators);
            return Utils.ToResult(collaborators.Select(c => new
            {
                id = c.Id,
                login = c.Login,
                avatar_url = c.AvatarUrl,
                url = c.Url,
            }), pageNo);
        }

        internal IssueRequest IssueRequest(IssueRequest req, string filter, string state, IEnumerable<string> labels, string sort, string direction, DateTime? since)
        {
            if (!string.IsNullOrEmpty(filter))
            {
                switch(filter.ToLower())
                {
                    case "assigned": req.Filter = IssueFilter.Assigned;break;
                    case "created": req.Filter = IssueFilter.Created; break;
                    case "mentioned": req.Filter = IssueFilter.Mentioned; break;
                    case "subscribed": req.Filter = IssueFilter.Subscribed; break;
                    case "all": req.Filter = IssueFilter.All; break;
                } 
            }

            if (!string.IsNullOrEmpty(state))
            {
                switch (state.ToLower())
                {
                    case "open": req.State = ItemStateFilter.Open; break;
                    case "closed": req.State = ItemStateFilter.Closed; break;
                    case "all": req.State = ItemStateFilter.All; break;
                }
            }

            if (null != labels && labels.Count() > 0)
                foreach (var label in labels)
                    req.Labels.Add(label);

            if (!string.IsNullOrEmpty(sort))
            {
                switch (sort.ToLower())
                {
                    case "created": req.SortProperty = IssueSort.Created; break;
                    case "updated": req.SortProperty = IssueSort.Updated; break;
                    case "comments": req.SortProperty = IssueSort.Comments; break;
                }
            }

            if (!string.IsNullOrEmpty(direction))
            {
                switch (direction.ToLower())
                {
                    case "asc": req.SortDirection = SortDirection.Ascending; break;
                    case "desc": req.SortDirection = SortDirection.Descending; break;
                }
            }

            if (since.HasValue)
            {
                req.Since = new DateTimeOffset(since.Value);
            }
            return req;
        }

        internal string StringParam(string p)
        {
            string s = p?.Trim();
            return string.IsNullOrEmpty(s) ? null : s;
        }

        internal RepositoryIssueRequest RepositoryIssueRequest(RepositoryIssueRequest req, string milestone, string assignee, string creator, string mentioned)
        {

            req.Milestone = StringParam(milestone);
            req.Assignee = StringParam(assignee);
            req.Creator = StringParam(creator);
            req.Mentioned = StringParam(mentioned);
            return req;
        }
        internal class IssuesWrapper
        {
            public long? repo_id;
            public IReadOnlyList<Issue> issues;
        }
        internal async Task<Model.ResultPage> GetIssues(IEnumerable<long?> repository_ids,
            string filter = "all",
            string state = "all",
            IEnumerable<string> labels = null,
            DateTime? since = null,
            string sort = "created",
            string direction = "desc",

            string milestone = null,
            string assignee = null,
            string creator = null,
            string mentioned = null,
            int? pageSize = null,
            int? pageNo = null)
        {
            List<IssuesWrapper> issues = null;
            if (null != repository_ids && repository_ids.Count() > 0)
            {
                RepositoryIssueRequest reposotoryIssueRequest = new RepositoryIssueRequest();
                IssueRequest(reposotoryIssueRequest, null, state, labels, sort, direction, since);
                RepositoryIssueRequest(reposotoryIssueRequest, milestone, assignee, creator, mentioned);
                issues = (await Task.WhenAll(repository_ids.Select(async r =>
                    new IssuesWrapper
                    {
                        repo_id = r,
                        issues = await _githubCli.Issue.GetAllForRepository(r.Value, reposotoryIssueRequest,
                            Utils.ApiOptions(pageSize, pageNo))
                    }))).ToList();
            }
            else
            {
                issues = new List<IssuesWrapper>();
                IssueRequest issueRequest = new IssueRequest()
                {

                };
                IssueRequest(issueRequest, filter, state, labels, sort, direction, since);
                issues.Add(new IssuesWrapper()
                {
                    repo_id = null,
                    issues = await _githubCli.Issue.GetAllForCurrent(issueRequest, Utils.ApiOptions(pageSize, pageNo))
                });
            }

            //foreach(var i in issues) Utils.Save2File($"05_repo_issues_{i?.repo_id ?? 0}.json", i.issues.Where(ii => ii == i.issues.Skip(1).First()));
            //foreach (var i in issues) Utils.Save2File($"05_repo_issues_{i?.repo_id ?? 0}.json", i.issues));
            return Utils.ToResult(issues.SelectMany(i => i.issues, 
                (repo, i) => new
                {
                    id = i.Id,
                    number = i.Number,
                    state = i.State.StringValue,
                    title = i.Title,
                    assignee = i.Assignee?.Name,
                    repo_id = i?.Repository?.Id ?? repo.repo_id
                }), pageNo);
        }


        /// <summary>
        /// search for issues within specific repo
        /// </summary>
        /// <param name="repository_id">repository identifier</param>
        /// !<param name="filter">Indicates which sorts of issues to return. Can be one of: assigned, created, mentioned, subscribed, all. Default: all</param>
        /// <param name="state">Indicates the state of the issues to return. Can be either open, closed, or all. Default: all</param>
        /// <param name="labels">A list of comma separated label names. Example: bug,ui,@high</param>
        /// <param name="since">Only issues updated at or after this time are returned. This is a timestamp in ISO 8601 format: YYYY-MM-DDTHH:MM:SSZ</param>
        /// <param name="sort">What to sort results by. Can be either created, updated, comments. Default: created</param>
        /// <param name="direction">The direction of the sort. Can be either asc or desc. Default: desc</param>
        /// <param name="milestone">If an integer is passed, it should refer to a milestone by its number field. If the string * is passed, issues with any milestone are accepted. If the string none is passed, issues without milestones are returned.</param>
        /// <param name="assignee">Can be the name of a user. Pass in none for issues with no assigned user, and * for issues assigned to any user.</param>
        /// <param name="creator">The user that created the issue.</param>
        /// <param name="mentioned">A user that's mentioned in the issue.</param>
        /// <param name="pageSize">page size (items per page)</param>
        /// <param name="pageNo">page number to return</param>
        /// <returns></returns>
        [HttpGet("{repository_id}/issues", Name = "GetIssuesRepo")]
        [Authorize]
        public async Task<Model.ResultPage> GetIssuesRepo([FromRoute]long? repository_id,
            [FromQuery]string filter = "all",
            [FromQuery]string state = "all",
            [FromQuery]string[] labels = null,
            [FromQuery]DateTime? since = null,
            [FromQuery]string sort = "created",
            [FromQuery]string direction = "desc",

            [FromQuery]string milestone = null,
            [FromQuery]string assignee = null,
            [FromQuery]string creator = null,
            [FromQuery]string mentioned = null,
            [FromQuery]int? pageSize = null,
            [FromQuery]int? pageNo = null)
        {
            return await GetIssues(new List<long?>() { repository_id }, filter, state, labels, since, sort, direction, milestone, assignee, creator, mentioned, pageSize, pageNo);
        }

        /// <summary>
        /// search for issues
        /// </summary>
        /// !<param name="filter">Indicates which sorts of issues to return. Can be one of: assigned, created, mentioned, subscribed, all. Default: all</param>
        /// <param name="state">Indicates the state of the issues to return. Can be either open, closed, or all. Default: all</param>
        /// <param name="labels">A list of comma separated label names. Example: bug,ui,@high</param>
        /// <param name="since">Only issues updated at or after this time are returned. This is a timestamp in ISO 8601 format: YYYY-MM-DDTHH:MM:SSZ</param>
        /// <param name="sort">What to sort results by. Can be either created, updated, comments. Default: created</param>
        /// <param name="direction">The direction of the sort. Can be either asc or desc. Default: desc</param>
        /// <param name="milestone">If an integer is passed, it should refer to a milestone by its number field. If the string * is passed, issues with any milestone are accepted. If the string none is passed, issues without milestones are returned.</param>
        /// <param name="assignee">Can be the name of a user. Pass in none for issues with no assigned user, and * for issues assigned to any user.</param>
        /// <param name="creator">The user that created the issue.</param>
        /// <param name="mentioned">A user that's mentioned in the issue.</param>
        /// <param name="pageSize">page size (items per page)</param>
        /// <param name="pageNo">page number to return</param>
        /// <returns></returns>
        [HttpGet("issues", Name = "GetIssuesRepos")]
        [Authorize]
        public async Task<Model.ResultPage> GetIssuesRepos([FromQuery]long?[] repository_ids,
            [FromQuery]string filter = "all",
            [FromQuery]string state = "all",
            [FromQuery]string[] labels = null,
            [FromQuery]DateTime? since = null,
            [FromQuery]string sort = "created",
            [FromQuery]string direction = "desc",

            [FromQuery]string milestone = null,
            [FromQuery]string assignee = null,
            [FromQuery]string creator = null,
            [FromQuery]string mentioned = null,
            [FromQuery]int? pageSize = null,
            [FromQuery]int? pageNo = null)
        {
            return await GetIssues(repository_ids, filter, state, labels, since, sort, direction, milestone, assignee, creator, mentioned, pageSize, pageNo);
        }
    }
}