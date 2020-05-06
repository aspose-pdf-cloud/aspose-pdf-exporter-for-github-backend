using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter
{
    /// <summary>
    /// Extracts token from Authorization headers, creates claim with extracted token
    /// </summary>
    public class AccessTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AccessTokenAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.NoResult();

            string accessToken = null;
            try
            {
                var authHeaderValue = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
                
                Regex regexp = new Regex(@"\S+\s+(?<token>\S+)", RegexOptions.IgnoreCase);
                Match match = regexp.Match(authHeaderValue.ToString());
                if (match.Success)
                    accessToken = match.Groups["token"].Value;
            }
            catch
            {
                return AuthenticateResult.NoResult();
            }

            if (accessToken == null)
                return AuthenticateResult.NoResult();

            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, "john_doe"),
                new Claim(ClaimTypes.Name, "John Doe"),
                new Claim("Authorization", accessToken),
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}
