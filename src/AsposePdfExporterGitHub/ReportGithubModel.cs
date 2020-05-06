using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Octokit;
namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter
{
    /// <summary>
    /// Transforms issues data received from Github to data model suitable for reports generation
    /// </summary>
    public class IssueData
    {
        public long IssueRepoId { get; set; }
        public int IssueNumber { get; set; }
        public Issue Issue { get; set; }
        public Repository IssueRepo { get; set; }

        public IReadOnlyList<IssueComment> IssueComments { get; set; }
        public static async Task<IssueData> Fetch(IGitHubClient cli, long repoId, int issueNo)
        {
            IssueData result = new IssueData
            {
                IssueRepoId = repoId
                , IssueNumber = issueNo
                , Issue = await cli.Issue.Get(repoId, issueNo)
            };
            if (result.Issue?.Comments > 0)
                result.IssueComments = await cli.Issue.Comment.GetAllForIssue(repoId, issueNo);
            return result;
        }
    }
    public class ReportGithubModel : Report.YamlReportModel
    {
        public ReportGithubModel(string yamlTemplateContent) : base(yamlTemplateContent) { }
        
        /// <summary>
        /// Transforms Github issues into report's model
        /// </summary>
        /// <param name="issues"></param>
        /// <returns></returns>
        public dynamic issuesModel(List<IssueData> issues)
        {
            JToken issue_name(IssueData d) => d?.Issue?.Number;
            string toShortDate(DateTimeOffset? d) {
                return d?.Date.ToShortDateString();
            }



            List<string> getTextLines(string text) =>
                text.Split(new[] {'\n'})
                    .Select(s => s.Trim(new[] {'\r'}))
                    .ToList();

            dynamic property<T>(T o, Func<T, bool> ne) => new
            {
                Value = o,
                NotEmpty = ne(o),
                Empty = !ne(o),
            };
            
            return new
            {
                issues = issues.Select(i => new
                {
                    issueName = issue_name(i),
                    projectName = i?.IssueRepo?.FullName,
                    //labels = null == i?.Issue?.Labels ? null : string.Join(", ", i?.Issue?.Labels.Select(l => l.Name)),
                    summary = i?.Issue?.Title,
                    reporter = i?.Issue?.User?.Login,
                    assignee = i?.Issue?.Assignee?.Login,
                    detailsLines = property(getTextLines(i?.Issue?.Body), x => x.Count > 0),
                    milestone = i?.Issue?.Milestone?.Title,
                    state = i?.Issue.State.StringValue,
                    created = toShortDate(i?.Issue?.CreatedAt),
                    updated = toShortDate(i?.Issue?.UpdatedAt),
                    closed = toShortDate(i?.Issue?.ClosedAt),

                    assigneeList = property(i?.Issue?.Assignees?.Select(l => l.Login), x => x.Count() > 1),
                    issueLabelsList = property(i?.Issue?.Labels?.Select(l => l.Name), x => x.Count() > 0),
                    
                    issueQrImage = QueryHelpers.AddQueryString("file://issue-link-qr", "link", i?.Issue?.HtmlUrl),
                    issueQrImageVisible = GenerateQRCode,
                    issueLink = i?.Issue?.HtmlUrl,

                    commentsNotEmpty = i?.Issue?.Comments > 0,
                    comments = null == i?.IssueComments ? null : i?.IssueComments.Select(c => new
                    {
                        commentAuthor = c?.User?.Login,
                        commentCreated = toShortDate(c?.CreatedAt),
                        commentTextLines = property(getTextLines(c?.Body), x => x.Count > 0),
                    }),
                    reactionsNotEmpty = i?.Issue?.Reactions?.TotalCount > 0,
                    reactions = null == i?.Issue?.Reactions ? null : new
                    {
                        plus1 = i?.Issue?.Reactions?.Plus1,
                        minus1 = i?.Issue?.Reactions?.Minus1,
                        laugh = i?.Issue?.Reactions?.Laugh,
                        confused = i?.Issue?.Reactions?.Confused,
                        heart = i?.Issue?.Reactions?.Heart,
                        hooray = i?.Issue?.Reactions?.Hooray,
                    },
                })
            };
        }
    }
}
