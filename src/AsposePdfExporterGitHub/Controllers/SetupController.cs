using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Services;
using Aspose.Cloud.Marketplace.Common;
using Aspose.Cloud.Marketplace.Services;
using Aspose.Cloud.Marketplace.Services.Model.Elasticsearch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.ModelExtension;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Controllers
{
    /// <summary>
    /// Implements utility endpoints
    /// </summary>
    public class SetupController : ControllerBase
    {
        private readonly ILogger<SetupController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _clientFactory;

        public SetupController(ILogger<SetupController> logger, IConfiguration configuration, IHttpClientFactory clientFactory)
        {

            _logger = logger;
            _configuration = configuration;
            _clientFactory = clientFactory;
        }
        /// <summary>
        /// Finish OAuth flow by exchanging code with token
        /// </summary>
        /// <param name="code"></param>
        /// <param name="redirect_uri"></param>
        /// <param name="state"></param>
        /// <param name="cli"></param>
        /// <returns></returns>
        [HttpGet("/token", Name = "GetToken")]
        public async Task<IActionResult> GetTokenAsync(string code, string redirect_uri, string state, [FromServices]IAppGithubExporterCli cli)
        {
            var client = _clientFactory.CreateClient("github.com");
            var requestUri = $"login/oauth/access_token";
            
            using (var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri))
            {
                var postParams = new Dictionary<string, string> { 
                    { "client_id", _configuration.GetValue<string>("GithubApp:ClientId") }
                    , { "client_secret", _configuration.GetValue<string>("GithubApp:ClientSecret") }
                    , { "code", code }
                    , { "redirect_uri", redirect_uri }
                    , { "state", state }
                };

                requestMessage.Content = new FormUrlEncodedContent(postParams);
                using var response = await client.SendAsync(requestMessage);
                string responseData = await response.Content.ReadAsStringAsync();
                var responseValues = QueryHelpers.ParseQuery(responseData);
                //_logger.LogDebug("Received token: ", responseData);
                if (responseValues.ContainsKey("error"))
                    throw new ControllerException(responseValues.GetValueOrDefault("error").FirstOrDefault(), code: HttpStatusCode.BadRequest, customData:
                        await new ZipFileArchive().AddFile("01_params.json", new
                            {
                                error = responseValues.GetValueOrDefault("error").FirstOrDefault()
                                , error_description = responseValues.GetValueOrDefault("error_description").FirstOrDefault()
                                , responseData
                            }).AddFile("02_post_params.json", postParams)
                            .Archive()
                    );
                
                if (!responseValues.ContainsKey("access_token"))
                    throw new ControllerException("response_invalid", code: HttpStatusCode.BadRequest, customData:
                        await new ZipFileArchive().AddFile("01_params.json", new
                            {
                                error = "response_invalid", error_description = "Invalid response data", responseData
                            }).AddFile("02_post_params.json", postParams)
                            .Archive()
                    );
                var access_token = responseValues["access_token"].FirstOrDefault();
                cli.GitHubClient.Connection.Credentials = new Credentials(access_token);
                var emails = await cli.GitHubClient.User.Email.GetAll();
                var user = await cli.GitHubClient.User.Current();

                return new OkObjectResult(new
                {
                    access_token = access_token,
                    token_type = responseValues["token_type"].FirstOrDefault(),
                    user_name = user.Name,
                    email = emails.FirstOrDefault(e => e.Primary)?.Email,
                    avatar_url = user.AvatarUrl,
                    url = user.Url,
                    manage_app_permissions_url = $"https://github.com/settings/connections/applications/{_configuration.GetValue<string>("GithubApp:ClientId")}"
                });
            }
        }
        /// <summary>
        /// Webhook handles Github events
        /// </summary>
        /// <param name="event">Event name</param>
        /// <param name="delivery">action id</param>
        /// <param name="signature">request signature</param>
        /// <param name="cli">client (from services)</param>
        /// <param name="remoteLogger">remote logger (from services)</param>
        /// <returns></returns>
        [HttpPost("/webhook", Name = "PostWebook")]
        public async Task<IActionResult> PostWebook([FromHeader(Name = "X-GitHub-Event")]string @event
            , [FromHeader(Name = "X-GitHub-Delivery")]string delivery
            , [FromHeader(Name = "X-Hub-Signature")]string signature
            , [FromServices] IAppGithubExporterCli cli, [FromServices]ILoggingService remoteLogger)
        {
            
            byte[] body;
            string json;
            await using (var ms = new MemoryStream())
            {
                await Request.Body.CopyToAsync(ms);
                body = ms.ToArray();
                json = Encoding.UTF8.GetString(body);
            }
            var secret = _configuration.GetValue<string>("GithubApp:WebhookEventSecret");
            if (null != secret)
            {
                HMACSHA1 hmac = new HMACSHA1(Encoding.ASCII.GetBytes(secret));
                string calculatedSignature =
                    $"sha1={string.Concat(hmac.ComputeHash(body).Select(x => x.ToString("x2")))}".ToLower();
                if (signature?.ToLower() != calculatedSignature)
                    return new BadRequestObjectResult("Signature mismatch");
            }

            var serializer = new Octokit.Internal.SimpleJsonSerializer();
            _logger.LogInformation("{event} content: {json}", @event.ToLower(), json);
            switch (@event?.ToLower())
            {
                case "ping":
                    var ping = serializer.Deserialize<PingEvent>(json);
                    _logger.LogInformation($"PINGed by {ping?.Sender?.Login}");
                    break;
                case "marketplace_purchase":
                    var purchase = serializer.Deserialize<MarketplacePurchaseEvent>(json);
                    var document = purchase.ToSetupLogDocument(@event?.ToLower(), cli.RequestId,
                        cli.AppName, delivery?.ToLower());
                    await remoteLogger.ReportSetupLog(document);
                    break;
                default:
                    _logger.LogError("Unknown webhook event: `{event}`", @event);
                    break;
            }
            return new OkResult();
        }


    }

    public static class MarketplacePurchaseEventExtension
    {
        public static ElasticsearchSetupDocument ToSetupLogDocument(this MarketplacePurchaseEvent e, string @event, string requestId, string appName, string actionId)
        {
            return new ElasticsearchSetupDocument(id:requestId, logName:"setup_log", appName: appName, action:$"{@event}-{e.Action}", actionId:actionId, actionOriginator:e?.Sender?.Login
                , actionDate:e.EffectiveDate, subscriber:e?.MarketplacePurchase?.Account?.Login, message:null, path:"/webhook", controllerName:"Setup", actionName: "PostWebook"
                , elapsedSeconds:null, parameters:null, resultCode:200);
        }
    }

}