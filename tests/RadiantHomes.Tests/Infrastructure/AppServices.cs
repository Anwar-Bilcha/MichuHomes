using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Services.Security;

namespace RadiantHomes.Tests.Infrastructure
{
    /// <summary>The app's access-control services wired against the test database, acting as a given user.</summary>
    public sealed class AppServices : IAsyncDisposable
    {
        private readonly ServiceProvider _root;
        private readonly AsyncServiceScope _scope;

        public AppServices(TestDatabase database, string? actingUserId = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMemoryCache();
            services.AddDataProtection();
            var http = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = actingUserId is null
                        ? new ClaimsPrincipal(new ClaimsIdentity())
                        : new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, actingUserId), new Claim(ClaimTypes.Name, "tester") }, "Test")),
                },
            };
            services.AddSingleton<IHttpContextAccessor>(http);
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(database.ConnectionString));
            services.AddIdentityCore<AppUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
            services.AddScoped<AccessControlService>();
            services.AddScoped<AuditService>();
            services.AddScoped<AccessManagementService>();
            services.AddScoped<CompanyLifecycleService>();
            _root = services.BuildServiceProvider();
            _scope = _root.CreateAsyncScope();
        }

        public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();
        public ApplicationDbContext Db => Get<ApplicationDbContext>();

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _root.DisposeAsync();
        }
    }
}
