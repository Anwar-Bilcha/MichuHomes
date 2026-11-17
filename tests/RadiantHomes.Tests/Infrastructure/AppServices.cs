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
            // Foundation 2
            services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(new TestEnvironment());
            services.AddScoped<RadiantHomes.Services.NotificationService.INotificationService, RadiantHomes.Services.NotificationService.NotificationService>();
            services.AddScoped<RadiantHomes.Services.CustomerPortal.CustomerNotifications>();
            services.AddScoped<RadiantHomes.Services.Settings.PlatformSettings>();
            services.AddScoped<RadiantHomes.Services.CustomerPortal.ReservationService>();
            services.AddScoped<RadiantHomes.Services.Kyc.KycService>();
            services.AddScoped<RadiantHomes.Services.Properties.BulkUploadService>();
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

    /// <summary>App root in a temporary folder (KYC documents are written under App_Data there).</summary>
    public sealed class TestEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public TestEnvironment()
        {
            ContentRootPath = Path.Combine(Path.GetTempPath(), "michu-tests", Guid.NewGuid().ToString("N"));
            WebRootPath = Path.Combine(ContentRootPath, "wwwroot");
            Directory.CreateDirectory(WebRootPath);
        }
        public string WebRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ApplicationName { get; set; } = "RadiantHomes";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; } = "Development";
    }
}
