using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Aspose.Cloud.Marketplace.Common;
using System.Net;
using System.Text;
using Aspose.BarCode.Cloud.Sdk.Model.Requests;
using Aspose.Cloud.Marketplace.Report;
using Aspose.Cloud.Marketplace.Services;
using Aspose.Cloud.Marketplace.Services.Model.Elasticsearch;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Services
{
    /// <summary>
    /// Client service implementation
    /// </summary>
    public class GithubExporterClientService : IAppGithubExporterCli
    {
        public class BarcodeApiStub : IBarcodeApi
        {
            readonly Aspose.BarCode.Cloud.Sdk.BarCodeApi _instance;

            public BarcodeApiStub(Aspose.BarCode.Cloud.Sdk.BarCodeApi instance)
            {
                _instance = instance;
            }

            public void BarCodePutBarCodeGenerateFile(BarCodePutBarCodeGenerateFileRequest request) =>
                _instance.BarCodePutBarCodeGenerateFile(request);
        }

        internal const string AsposeClientHeaderName = "x-aspose-client";
        internal const string AsposeClientVersionHeaderName = "x-aspose-client-version";

        public string RequestId { get; }
        public string AppName { get; }
        public List<StatisticalDocument> Stat { get; set; }

        public double? ElapsedSeconds => stopwatch?.Elapsed.TotalSeconds;

        private readonly string _token;
        private Octokit.GitHubClient _githubClient;
        private Aspose.Pdf.Cloud.Sdk.Api.PdfApi _pdfApi;
        private BarcodeApiStub _barcodeApiStub;
        private readonly Stopwatch stopwatch;
        private readonly Aspose.Pdf.Cloud.Sdk.Client.Configuration _pdfConfig;
        private readonly Aspose.BarCode.Cloud.Sdk.Configuration _barcodeConfig;

        public GithubExporterClientService(string appName, string token, string apiKey, string appSid, string basePath = "", bool debug = false)
        {
            AppName = appName;
            RequestId = Guid.NewGuid().ToString();
            
            _token = token;
            _githubClient = null;
            _pdfApi = null;
            _barcodeApiStub = null;

            Stat = new List<StatisticalDocument>();

            var version = GetType().Assembly.GetName().Version;
            var DefaultHeaders = new Dictionary<string, string>
            {
                {AsposeClientHeaderName, AppName},
                {AsposeClientVersionHeaderName, $"{version.Major}.{version.Minor}"}
            };
            _pdfConfig = new Aspose.Pdf.Cloud.Sdk.Client.Configuration(apiKey, appSid)
            {
                DefaultHeader = DefaultHeaders
            };
            if (!string.IsNullOrEmpty(basePath))
                _pdfConfig.BasePath = basePath;

            _barcodeConfig = new Aspose.BarCode.Cloud.Sdk.Configuration
            {
                DebugMode = debug,
                AppKey = apiKey,
                AppSid = appSid,
                DefaultHeaders = DefaultHeaders
            };
            if (!string.IsNullOrEmpty(basePath))
                _barcodeConfig.ApiBaseUrl = basePath;

            stopwatch = new Stopwatch();
            stopwatch.Start();
        }

        public Octokit.IGitHubClient GitHubClient
        {
            get
            {
                if (null != _githubClient)
                    return _githubClient;
                _githubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue(Assembly.GetExecutingAssembly().GetName().Name));
                if (!string.IsNullOrEmpty(_token))
                    _githubClient.Credentials = new Octokit.Credentials(_token);
                return _githubClient;
            }
        }

        public Aspose.Pdf.Cloud.Sdk.Api.IPdfApi PdfApi => _pdfApi ??= new Aspose.Pdf.Cloud.Sdk.Api.PdfApi(_pdfConfig);
        public IBarcodeApi BarcodeApi => _barcodeApiStub ??= (_barcodeApiStub = new BarcodeApiStub(new Aspose.BarCode.Cloud.Sdk.BarCodeApi(_barcodeConfig)));

        public ValueTuple<int, string, string, byte[]> ErrorResponseInfo(Exception ex)
        {
            HttpStatusCode code = HttpStatusCode.InternalServerError;
            byte[] customData = null;
            string text = "General error";
            if (null != ex)
            {
                text = ex.Message;
                code = ControllerException.StatusCode(ex);
                if (ex is ControllerException cex)
                {
                    code = cex.Code;
                    customData = cex.CustomData;
                }
            }
            return ((int)code, code.ToString(), text, customData);
        }

        public async Task<string> ReportException(ElasticsearchErrorDocument doc, IServiceProvider serviceProvider)
        {
            var ctx = serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var basePathReplacement = serviceProvider.GetRequiredService<IBasePathReplacement>();
            var linkGenerator = serviceProvider.GetRequiredService<LinkGenerator>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            string errorFileId = $"{RequestId}-error";
            string result = basePathReplacement.ReplaceBaseUrl(linkGenerator.GetUriByAction(ctx, "GetDownload", "Export", values: new { id = errorFileId, error = true }));
            await using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(doc))))
            {
                string errorFileStoragePath = $"{configuration.GetValue("Settings:StorageRoot", "clients_github")}/{errorFileId}.json";
                var uploadResult = await PdfApi.UploadFileAsync(errorFileStoragePath, ms, configuration.GetValue<string>("Settings:StorageName"));
                if (null != uploadResult.Errors && uploadResult.Errors.Count > 0)
                {
                    var logger = serviceProvider.GetRequiredService<ILogger>();
                    logger.LogError($"Error occured while uploading file {errorFileStoragePath}. Error:{string.Join(";", uploadResult.Errors.Select(e =>e.Message))}");
                    result = null;
                }
            }

            return result;
        }
    }
}
