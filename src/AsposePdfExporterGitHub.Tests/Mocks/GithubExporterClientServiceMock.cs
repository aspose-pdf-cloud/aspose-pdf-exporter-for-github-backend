using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aspose.Cloud.Marketplace.Common;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Services;
using Aspose.Cloud.Marketplace.Report;
using Aspose.Cloud.Marketplace.Services.Model.Elasticsearch;
using Aspose.Pdf.Cloud.Sdk.Api;
using Moq;
using Octokit;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks
{
    /// <summary>
    /// Contains IAppGithubExporterCli mocking implementation.
    /// You can pass mocks for GitHubClientMock, PdfApiMock, BarcodeApiMock
    /// </summary>
    public class GithubExporterClientServiceMock : IAppGithubExporterCli
    {
        internal Mock<IPdfApi> PdfApiMock;
        internal Mock<IBarcodeApi> BarcodeApiMock;

        public GitHubClientMock GitHubClientMock;

        public GithubExporterClientServiceMock(GitHubClientMock githubClientMock = null, Mock<IPdfApi> pdfApiMock = null, Mock<IBarcodeApi> barcodeApiMock = null)
        {
            ElapsedSeconds = 1;
            GitHubClientMock = githubClientMock;
            PdfApiMock = pdfApiMock;
            BarcodeApiMock = barcodeApiMock;
            if (null != PdfApiMock)
            {
                PdfApiMock = ExportControllerFixture.SetupDownload(PdfApiMock);
            }

        }
        public string RequestId => $"mock_{Guid.NewGuid()}";
        public string AppName => "appmock";
        public double? ElapsedSeconds { get; set; }

        public List<StatisticalDocument> Stat { get; set; }
        public ValueTuple<int, string, string, byte[]> ErrorResponseInfo(Exception ex)
        {
            return (-1, "mock_exception", "mock_exception text", null);
        }

        public IGitHubClient GitHubClient => GitHubClientMock.MockGitHubClient.Object;
        
        public IPdfApi PdfApi => PdfApiMock.Object;

        public IBarcodeApi BarcodeApi => BarcodeApiMock.Object;

        public async Task<string> ReportException(ElasticsearchErrorDocument doc, IServiceProvider serviceProvider)
        {
            return await Task.FromResult("error_log.zip");
        }
    }
}
