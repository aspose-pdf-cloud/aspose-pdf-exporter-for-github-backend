using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Octokit;
using System.IO;
using System.Net;
using Aspose.Cloud.Marketplace.Services;
using Aspose.Cloud.Marketplace.Common;
using ReportModel = Aspose.Cloud.Marketplace.Report.Model;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Controllers
{
    /// <summary>
    /// Implements export and download functions
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ExportController : ControllerBase
    {
        private readonly ILogger<ExportController> _logger;
        private readonly Services.IAppGithubExporterCli _client;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _hostEnvironment;
        private readonly IBasePathReplacement _basePathReplacement;

        private IGitHubClient _githubCli => _client?.GitHubClient;

        public ExportController(ILogger<ExportController> logger, Services.IAppGithubExporterCli client, IConfiguration configuration
            , IWebHostEnvironment hostEnvironment, IBasePathReplacement basePathReplacement)
        {
            _logger = logger;
            _client = client;
            _configuration = configuration;
            _hostEnvironment = hostEnvironment;
            _basePathReplacement = basePathReplacement;
        }
        /// <summary>
        /// Generates PDF report based on input issues
        /// </summary>
        /// <param name="issuesList">Issues list</param>
        /// <param name="generateQr">generate or not QR code for an issue (true or false) default: true</param>
        /// <returns></returns>
        [Authorize]
        [HttpPost("export", Name = "PostExport")]
        public async Task<IActionResult> PostExport([FromBody]List<Model.IssueListItem> issuesList, [FromQuery]bool generateQr = true)
        {
            User user = null;
            List<IssueData> issues = null;
            Dictionary<long, Repository> repo_dict = null;
            ReportModel.Document documentReportModel = null;

            string extension = "pdf";
            string uid = Guid.NewGuid().ToString();
            string reportFileName = $"{_configuration.GetValue<string>("Settings:StorageRoot", "clients_github")}/{uid}.{extension}";
            Report.PdfReport pdfReport = null;
            try
            {
                user = await _githubCli.User.Current();
                //Utils.Save2File("07_user.json", user);
                // fetch issues
                issues = (await Task.WhenAll(issuesList.Select(i => IssueData.Fetch(_githubCli, i.RepositoryId, i.IssueNo)))).ToList();
                // fetch repositories
                repo_dict = (await Task.WhenAll(issues.GroupBy(i => i.IssueRepoId).Select(i => _githubCli.Repository.Get(i.Key)))).ToDictionary(r => r.Id, r => r);
                // update issues repositories
                issues.ForEach(i => i.IssueRepo = repo_dict[i.IssueRepoId]);
                ReportGithubModel model = new ReportGithubModel(System.IO.File.ReadAllText(_configuration.GetValue("Templates:ReportIssuesModel", "template/Report-Issues.Mustache")));
                model.GenerateQRCode = generateQr;
                documentReportModel = model.CreateReportModel(model.issuesModel(issues));
                if (null == documentReportModel.Options)
                    documentReportModel.Options = new ReportModel.DocumentOptions();

                pdfReport = new Report.PdfReport(filePath: reportFileName, storageName: _configuration.GetValue<string>("Settings:StorageName"), debug: _hostEnvironment.IsDevelopment());
                await pdfReport.Configure(_client.PdfApi, _client.BarcodeApi);
                await pdfReport.Report(documentReportModel);
                return new OkObjectResult(new
                {
                    downloadlink = _basePathReplacement.ReplaceBaseUrl(Url.Link("GetDownload", new { id = uid })),
                    id = uid
                });
            }catch (Exception ex)
            {
                ZipFileArchive archive = new ZipFileArchive().AddFile("010_request_params.json", new
                {
                    RequestId = _client.RequestId,
                    FileId = uid,
                    FileName = reportFileName
                });
                foreach (var f in new Dictionary<string, object> {
                    { "020_user.json", user },
                    { "030_issues.json", issues },
                    { "040_repo_dict.json", repo_dict },
                    { "050_report_model.json", documentReportModel },
                    }.ToArray())
                    archive.AddFile(f.Key, f.Value);
                throw new ControllerException($"Error generating {reportFileName}", innerException: ex, customData : await archive.Archive());
            }
            finally
            {
                _client.Stat = pdfReport?.Stat;
            }
        }
        /// <summary>
        /// Downloads file by id
        /// </summary>
        /// <param name="id">file id</param>
        /// <param name="error">true for error file, false for pdf file</param>
        /// <returns></returns>
        [HttpGet("download/{id}", Name = "GetDownload")]
        public async Task<ActionResult> GetDownload([FromRoute]string id, [FromQuery]bool error = false)
        {
            string fileName = error ? $"{id}.json" : $"{id}.pdf";
            string fileDownloadName = error ? "Error.json" : "Issues.pdf";
            string contentType = error ? "application/json" : "application/pdf";
            string filePath = $"{_configuration.GetValue<string>("Settings:StorageRoot", "clients_github")}/{fileName}";
            var storageName = _configuration.GetValue<string>("Settings:StorageName");
            try
            {
                MemoryStream stream = new MemoryStream();
                var fileVersion = await _client.PdfApi.GetFileVersionsAsync(filePath, storageName: storageName);
                if (fileVersion?.Value != null && fileVersion?.Value.Count > 0)
                {
                    var ver = fileVersion.Value[0];
                    fileDownloadName = error ? $"Error-{ver.ModifiedDate:yyyy-MM-dd}.json" : $"Issues-{ver.ModifiedDate:yyyy-MM-dd}.pdf";
                }
                await using (var s = await _client.PdfApi.DownloadFileAsync(path: filePath, storageName: storageName))
                {
                    await s.CopyToAsync(stream);
                }
                stream.Position = 0;

                return new FileStreamResult(stream, contentType)
                {
                    FileDownloadName = fileDownloadName
                };
            } catch(Aspose.Pdf.Cloud.Sdk.Client.ApiException ex)
            {
                throw new ControllerException($"Error downloading {fileName}", code: (HttpStatusCode)ex.ErrorCode, innerException: ex);
            }
        }
    }
}