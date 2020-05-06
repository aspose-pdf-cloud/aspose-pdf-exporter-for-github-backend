using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octokit;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Controllers
{
    /// <summary>
    /// Github organization functions
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizationController : ControllerBase
    {
        private readonly Services.IAppGithubExporterCli _client;
        
        private IGitHubClient _githubCli => _client?.GitHubClient;
        public OrganizationController(Services.IAppGithubExporterCli client)
        {
            _client = client;
        }

        [HttpGet(Name = "GetOrgs")]
        [Authorize]
        public async Task<Model.ResultPage> GetOrgs([FromQuery]int? pageSize = null,
            [FromQuery]int? pageNo = null)
        {
            var orgs = await _githubCli.Organization.GetAllForCurrent(Utils.ApiOptions(pageSize, pageNo));
            //Utils.Save2File("01_org_getorgs.json", orgs);
            return Utils.ToResult(orgs.Select(o => new
            {
                id = o.Id,
                name = o.Login,
                avatar_url = o.AvatarUrl
            }), pageNo);
        }
    }
}