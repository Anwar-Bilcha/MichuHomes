using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RadiantHomes.Tests.Infrastructure
{
    /// <summary>
    /// The real app on the test database. Requests sign in as the account named in the X-Test-User header (a test
    /// authentication scheme replaces the cookie); without the header they are anonymous.
    /// </summary>
    public sealed class AppFactory : WebApplicationFactory<Program>
    {
        public const string UserHeader = "X-Test-User";
        private readonly TestDatabase _database;

        public AppFactory(TestDatabase database) => _database = database;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection", _database.ConnectionString);
            builder.ConfigureServices(services =>
            {
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, _ => { });
                services.PostConfigure<AuthenticationOptions>(o =>
                {
                    o.DefaultScheme = TestAuthHandler.Scheme;
                    o.DefaultAuthenticateScheme = TestAuthHandler.Scheme;
                    o.DefaultChallengeScheme = TestAuthHandler.Scheme;
                    o.DefaultForbidScheme = TestAuthHandler.Scheme;
                });
            });
        }

        public HttpClient ClientFor(string? userId)
        {
            var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            if (userId is not null) client.DefaultRequestHeaders.Add(UserHeader, userId);
            return client;
        }
    }

    public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string Scheme = "Test";

        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(AppFactory.UserHeader, out var id) || string.IsNullOrEmpty(id)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id!), new Claim(ClaimTypes.Name, "test-user") }, Scheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 403;
            return Task.CompletedTask;
        }
    }
}
