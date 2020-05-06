using System;
using System.Collections.Generic;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Services;
using Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests
{
    /// <summary>
    /// Base controller fixture passed by XUnit framework into each controller test
    /// </summary>
    /// <typeparam name="T">Controller class</typeparam>
    public class ControllerFixture<T> where T : class
    {
        public T Controller;
        public GitHubClientMock GitHubClientMock;
        public GithubExporterClientServiceMock ClientServiceMock;
        public Dictionary<string, string> Configuration;
        public virtual IServiceCollection ProvideServices(IServiceCollection c) =>
            c.AddLogging(c => { c.AddDebug(); })
                .AddScoped<IAppGithubExporterCli>(provider => ClientServiceMock)
                .AddSingleton<IConfiguration>(provider => new ConfigurationBuilder().AddInMemoryCollection(Configuration).Build())
                .AddSingleton(provider =>
                {
                    var moqHostEnvironment = new Mock<IWebHostEnvironment>();
                    moqHostEnvironment.Setup(h => h.EnvironmentName).Returns("Development");
                    return moqHostEnvironment.Object;
                });

        public virtual void Initialize()
        {
            throw new NotImplementedException(nameof(ControllerFixture<T>));
        }

        public ControllerFixture()
        {
            Configuration = new Dictionary<string, string>();
            Initialize();
            ClearInvocations();
            Controller = ActivatorUtilities.CreateInstance<T>(ProvideServices(new ServiceCollection()).BuildServiceProvider());
        }

        public virtual void ClearInvocations()
        {
            GitHubClientMock.ClearInvocations();
        }
    }

}
