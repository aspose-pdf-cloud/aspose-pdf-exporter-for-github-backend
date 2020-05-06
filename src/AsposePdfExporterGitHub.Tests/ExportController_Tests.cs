using System;
using Xunit;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Controllers;
using Aspose.Cloud.Marketplace.Report;
using Aspose.Cloud.Marketplace.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Aspose.Pdf.Cloud.Sdk.Api;
using Aspose.Pdf.Cloud.Sdk.Model;
using AutoFixture;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests
{
    public class ExportControllerFixture : ControllerFixture<ExportController>
    {
        public Mock<IPdfApi> PdfApiMock;
        public Mock<IBarcodeApi> BarcodeApiMock;
        public Mock<IUrlHelper> UrlHelperMock;
        public Mock<IBasePathReplacement> PathReplacementMock;
        public override void Initialize()
        {
            GitHubClientMock = new GitHubClientMock(mockAll: true);
            PdfApiMock = PdfExporter.Tests.PdfReport_Tests.Setup(PdfExporter.Tests.PdfReportPageProcessorFixture.Setup(new Mock<IPdfApi>()));
            PdfApiMock = SetupDownload(PdfApiMock);
            BarcodeApiMock = PdfExporter.Tests.PdfReportPageProcessorFixture.Setup(new Mock<IBarcodeApi>());

            UrlHelperMock = new Mock<IUrlHelper>();
            UrlHelperMock.Setup(x => x.Link("GetDownload", It.IsAny<object>()))
                .Returns((string routeName, object values) => $"http://localaddr/mockdownload/{values?.GetType().GetProperty("id")?.GetValue(values, null)}");

            PathReplacementMock = new Mock<IBasePathReplacement>();
            PathReplacementMock.Setup(e => e.ReplaceBaseUrl(It.IsAny<string>()))
                .Returns((string url) => url.Replace("localaddr", "mockaddr"));
            PathReplacementMock.Setup(e => e.ReplaceBaseUrl(It.IsAny<Uri>()))
                .Returns((Uri url) => new Uri(url.ToString().Replace("localaddr", "mockaddr")));


            Configuration = new Dictionary<string, string>()
            {
                {"Settings:StorageRoot", "mockroot"},
            };

            ClientServiceMock = new GithubExporterClientServiceMock(GitHubClientMock, PdfApiMock, BarcodeApiMock);
        }

        public override void ClearInvocations()
        {
            base.ClearInvocations();
            PdfApiMock.Invocations.Clear();
            BarcodeApiMock.Invocations.Clear();
            UrlHelperMock.Invocations.Clear();
            PathReplacementMock.Invocations.Clear();
        }

        public override IServiceCollection ProvideServices(IServiceCollection c) => base.ProvideServices(c)
            .AddSingleton(PathReplacementMock.Object);
        

        public static Mock<IPdfApi> SetupDownload(Mock<IPdfApi> PdfApiMock)
        {
            var fixture = new Fixture();
            PdfApiMock.Setup(f => f.DownloadFileAsync("mockroot/123-Mock-321.pdf", It.IsAny<string>(), It.IsAny<string>()))
                .Returns(async () => await Task.FromResult((Stream)new MemoryStream(Encoding.UTF8.GetBytes("file 123-Mock-321.pdf content"))));
            PdfApiMock.Setup(f => f.DownloadFileAsync("mockroot/123-ErrorMock-321.json", It.IsAny<string>(), It.IsAny<string>()))
                .Returns(async () => await Task.FromResult((Stream)new MemoryStream(Encoding.UTF8.GetBytes("file 123-ErrorMock-321.json content"))));

            PdfApiMock.Setup(f => f.GetFileVersionsAsync("mockroot/123-Mock-321.pdf", It.IsAny<string>()))
                .Returns(() => Task.FromResult(fixture.Create<FileVersions>()));

            return PdfApiMock;
        }
    }
    [Trait("Controllers", "ExportController")]
    public class ExportController_Tests : IClassFixture<ExportControllerFixture>
    {
        internal ExportControllerFixture Fixture;
        internal ExportController Controller;
        internal GitHubClientMock GitHubClientMock => Fixture.GitHubClientMock;
        internal Mock<IPdfApi> PdfApiMock => Fixture.PdfApiMock;
        internal Mock<IBarcodeApi> BarcodeApiMock => Fixture.BarcodeApiMock;
        internal GithubExporterClientServiceMock ClientServiceMock => Fixture.ClientServiceMock;
        public ExportController_Tests(ExportControllerFixture fixture)
        {
            Fixture = fixture;
            Fixture.ClearInvocations();
            Controller = Fixture.Controller;
            Controller.Url = Fixture.UrlHelperMock.Object;

        }

        [Fact]
        public async void PostExport_Test()
        {
            var r = await Controller.PostExport(
                GitHubClientMock.RepositoryIssuesData.Select((kv) => new Model.IssueListItem
                    {RepositoryId = kv.Key, IssueNo = kv.Value.Data.FirstOrDefault().Number}).ToList()
            );
            Assert.True(r is OkObjectResult, $"r should be OkObjectResult, not {r.GetType().Name}");
            var Result =  r as OkObjectResult;
            Assert.NotNull(Result);

            var resultStr = JsonConvert.SerializeObject(Result.Value);
            var result = JsonConvert.DeserializeObject<Dictionary<string, string>>(resultStr);
            Assert.Matches("http://mockaddr/mockdownload.*", result["downloadlink"]);
            Assert.Matches("[-a-zA-Z0-9]*", result["id"]);
        }
        [Fact]
        public async void GetDownload_Test()
        {
            var r = await Controller.GetDownload("123-Mock-321");
            Assert.True(r is FileStreamResult, $"r should be FileStreamResult, not {r.GetType().Name}");
            var Result = r as FileStreamResult;
            Assert.NotNull(Result);
            Assert.Equal("application/pdf", Result.ContentType);
            Assert.Matches("Issues-.{4}-.{2}-.{2}.pdf", Result.FileDownloadName);
            await using (var ms = new MemoryStream())
            {
                await Result.FileStream.CopyToAsync(ms);
                Assert.Equal("file 123-Mock-321.pdf content", Encoding.UTF8.GetString(ms.ToArray()));

            }
        }

        [Fact]
        public async void GetDownload_ErrorTest()
        {
            var r = await Controller.GetDownload("123-ErrorMock-321", true);
            Assert.True(r is FileStreamResult, $"r should be FileStreamResult, not {r.GetType().Name}");
            var Result = r as FileStreamResult;
            Assert.NotNull(Result);
            Assert.Equal("application/json", Result.ContentType);
            Assert.Matches("Error.json", Result.FileDownloadName);
            await using (var ms = new MemoryStream())
            {
                await Result.FileStream.CopyToAsync(ms);
                Assert.Equal("file 123-ErrorMock-321.json content", Encoding.UTF8.GetString(ms.ToArray()));

            }
        }
    }
}
