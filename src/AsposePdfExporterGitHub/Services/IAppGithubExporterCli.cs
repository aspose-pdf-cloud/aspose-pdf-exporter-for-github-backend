using Octokit;
using Aspose.Cloud.Marketplace.App.Middleware;
using Aspose.Cloud.Marketplace.Report;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Services
{
    /// <summary>
    /// Client service interface
    /// </summary>
    public interface IAppGithubExporterCli : IAppCustomErrorReportingClient
    {
        IGitHubClient GitHubClient { get; }
        Aspose.BarCode.Cloud.Sdk.Interfaces.IBarcodeApi BarcodeApi { get; }
        Aspose.Pdf.Cloud.Sdk.Api.IPdfApi PdfApi { get; }
    }
}
