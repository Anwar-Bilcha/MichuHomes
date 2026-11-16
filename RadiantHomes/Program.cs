using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using RadiantHomes.Areas.Identity.Pages.Account;
using RadiantHomes.DAL;
using RadiantHomes.Models;
using RadiantHomes.Services;
using QuestPDF.Infrastructure;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Serilog;
using RadiantHomes.LogConfig;
using Serilog.Core;
using RadiantHomes.Services.NotificationService;
using RadiantHomes.Services.AdminServices;
using RadiantHomes.Localization;
using RadiantHomes.Utilities;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//builder.Services.AddDefaultIdentity<AppUser>(options => options.SignIn.RequireConfirmedAccount = true).
//    AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddIdentity<AppUser, IdentityRole>().AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddHttpClient<ChapaPaymentService>();
builder.Services.AddScoped<IHouseServices,HouseServices>();
builder.Services.Configure<RadiantHomes.Models.Shared.TwilioSettings>(
    builder.Configuration.GetSection(RadiantHomes.Models.Shared.TwilioSettings.SectionName));
builder.Services.AddScoped<ISmsService, SmsService>();
builder.Services.AddScoped<ICustomerService,CustomerService>();
builder.Services.AddTransient<IEmailSender, DummyEmailSender>();
builder.Services.AddScoped<IAdminDashboard, AdminDashboardService>();
// Customer portal (Pages/Portal, Services/CustomerPortal)
builder.Services.AddScoped<RadiantHomes.Services.CustomerPortal.PortalUsers>();
builder.Services.AddScoped<RadiantHomes.Services.CustomerPortal.ApartmentCatalog>();
builder.Services.AddScoped<RadiantHomes.Services.CustomerPortal.CustomerNotifications>();
builder.Services.AddScoped<RadiantHomes.Services.CustomerPortal.ReservationService>();
builder.Services.AddScoped<RadiantHomes.Services.CustomerPortal.ApartmentComments>();
builder.Services.AddScoped<RadiantHomes.Services.CustomerPortal.ReferralService>();
// Video calls with agents (Pages/LiveVideoMeet, Services/VideoCalls). JaaS keys come from user-secrets or
// environment variables (Jaas__AppId, Jaas__ApiKeyId, Jaas__PrivateKey); without them the page says calls are off.
builder.Services.Configure<RadiantHomes.Services.VideoCalls.JaasSettings>(
    builder.Configuration.GetSection(RadiantHomes.Services.VideoCalls.JaasSettings.Section));
builder.Services.AddSingleton<RadiantHomes.Services.VideoCalls.JaasTokens>();
builder.Services.AddScoped<RadiantHomes.Services.VideoCalls.VideoCallService>();
builder.Services.AddMemoryCache();
//builder.Services.AddScoped<ExcelExportService>();
// Add services to the container.
//builder.Services.AddRazorPages();
// Access rules. Policies: MichuAdminOnly (platform admins), CompanyAdmin (platform or real-estate admins).
// Pages not listed here stay public (home, listings, details, registration).
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AccessPolicies.MichuAdminOnly, p => p.RequireRole(MichuHomesRoles.MichuAdmin));
    options.AddPolicy(AccessPolicies.CompanyAdmin, p => p.RequireRole(MichuHomesRoles.MichuAdmin, MichuHomesRoles.RealEstateAdmin));
});

// Areas/HousingCompanyAdmin/Pages/Index.cshtml is already reachable at /HousingCompanyAdmin by convention;
// an extra AddAreaPageRoute for the same URL made that request ambiguous (500), so none is added.
builder.Services.AddRazorPages(options =>
{
    var c = options.Conventions;

    // Platform administration
    c.AuthorizePage("/Admin/Dashboard/AdminDashboard", AccessPolicies.MichuAdminOnly);
    c.AuthorizeFolder("/Admin/Reports", AccessPolicies.MichuAdminOnly);
    c.AuthorizeFolder("/Admin/Notifications", AccessPolicies.CompanyAdmin);

    // Company administration
    c.AuthorizeAreaFolder("HousingCompanyAdmin", "/", AccessPolicies.CompanyAdmin);
    c.AuthorizeFolder("/RealEstates/OnBoarding", AccessPolicies.CompanyAdmin);
    c.AuthorizePage("/UploadPhotos", AccessPolicies.CompanyAdmin);
    c.AuthorizePage("/HouseOwner/Index", AccessPolicies.CompanyAdmin);
    foreach (var folder in new[] { "/Projects", "/Buildings", "/ApartmentHouses", "/RealEstates", "/RealEstates/Testimonials" })
    {
        foreach (var page in new[] { "Create", "Edit", "Delete" })
        {
            c.AuthorizePage($"{folder}/{page}", AccessPolicies.CompanyAdmin);
        }
    }
    c.AuthorizePage("/ApartmentHouses/Tour", AccessPolicies.CompanyAdmin); // rooms of an apartment's 3D tour
    c.AuthorizePage("/LiveVideoMeet/Join", AccessPolicies.CompanyAdmin);   // agents answering a customer's video call

    // Customer portal: any signed-in user (pages that need a customer profile ask for it)
    c.AuthorizeFolder("/Portal");

    // Signed-in users (house owners, buyers, tenants)
    c.AuthorizePage("/Houses/Create");
    c.AuthorizePage("/Houses/Edit");
    c.AuthorizePage("/Houses/Delete");
    c.AuthorizeFolder("/Houses/Actions");
});

// Languages: English source + Amharic (Resources/Translations/am.json, see Localization/)
builder.Services.AddMichuLocalization();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // You can set your preferred timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
var authentication = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login";
        options.LogoutPath = "/Identity/Account/Logout";
    });

// External sign-in only when its keys are configured: a provider with an empty ClientId/AppId makes every request
// fail. The login page and dialog list the registered providers, so their buttons disappear with them.
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = googleClientId;
        googleOptions.ClientSecret = googleClientSecret;
    });
}

var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
{
    authentication.AddFacebook(facebookOptions =>
    {
        facebookOptions.AppId = facebookAppId;
        facebookOptions.AppSecret = facebookAppSecret;
        facebookOptions.Scope.Add("email");
        facebookOptions.Fields.Add("email");
        facebookOptions.Scope.Add("email");
        facebookOptions.Scope.Add("public_profile");

        // Save access token (optional)
        facebookOptions.SaveTokens = true;

        facebookOptions.Fields.Add("name");
        facebookOptions.Fields.Add("email");
        facebookOptions.Fields.Add("picture"); // for profile image

        facebookOptions.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
        facebookOptions.ClaimActions.MapJsonKey("urn:facebook:picture", "picture", "string");
    });
}

// Data protection keys (sign-in cookies, antiforgery tokens, video call links) are kept in App_Data/keys so they
// survive restarts: on shared IIS the app pool often has no user profile, and in-memory keys would sign everyone out
// at every recycle. Falls back to the default store when that folder can't be written.
var keysFolder = new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys"));
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("MichuHomes");
var keysFolderWritable = StartupChecks.CanWrite(keysFolder);
if (keysFolderWritable) dataProtection.PersistKeysToFileSystem(keysFolder);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/AccessDenied"; // this is the key line
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
});
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddHttpContextAccessor();
var httpContextAccessor = new HttpContextAccessor(); // manual instance

builder.Services.AddHttpContextAccessor(); // Register for DI

// Plug in Serilog with enricher
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.With(new HttpContextUserNameEnricher(
        builder.Services.BuildServiceProvider().GetRequiredService<IHttpContextAccessor>()))
    .WriteTo.Console()
    .WriteTo.File(
        // Next to the app (on the host: \wwwroot\Logs\log-YYYYMMDD.txt), whatever the process's working folder is.
        path: Path.Combine(builder.Environment.ContentRootPath, "Logs", "log-.txt"),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({UserName}) {SourceContext} {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog(); // Plug into the host

builder.Services.AddSingleton<IHttpContextAccessor>(httpContextAccessor); // register it for DI too



builder.Host.UseSerilog(); // Plug into the host

// Anything that still stops the app (IIS then shows "500.30 app failed to start") is written to the log first.
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    if (e.ExceptionObject is Exception ex && ex.GetType().Name != "HostAbortedException") // EF tools stop the host that way
    {
        Log.Fatal(ex, "Michu Homes stopped because of an unhandled exception");
        Log.CloseAndFlush();
    }
};

var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error").UseAntiforgery();
    app.UseHsts();
    StartupChecks.LogMissingSettings(app.Configuration, app.Logger);
}
if (!keysFolderWritable)
{
    app.Logger.LogWarning("App_Data/keys can't be written: sign-ins will not survive a restart of the app.");
}

// The user roles must exist. A database that can't be reached must not stop the whole site (IIS 500.30 with no
// explanation): the site starts, pages that need the database show the error page, and the reason is logged.
try
{
    using var scope = app.Services.CreateScope();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roleNames = {
        MichuHomesRoles.IndividualHouseOwner,
        MichuHomesRoles.Buyer,
        MichuHomesRoles.Agent,
        MichuHomesRoles.RealEstateAdmin,
        MichuHomesRoles.MichuAdmin
    };

    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Cannot reach the database at startup. Check ConnectionStrings:DefaultConnection " +
        "(on the server: environment variable ConnectionStrings__DefaultConnection or appsettings.Production.json).");
}
// Uploaded ID documents live under wwwroot/uploads/ids but must not be public:
// they're only served through the authorised /HouseOwner/IdDocument page.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads/ids", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
// Apartment 3D models (.glb, see Pages/ApartmentHouses/ApartmentModel3D.cs) are not in the default list of
// served file types; without this they would be answered with 404.
var staticFileTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
staticFileTypes.Mappings[".glb"] = "model/gltf-binary";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = staticFileTypes });
app.UseHttpsRedirection();
app.UseMichuLocalization(); // picks the visitor's language and translates the rendered pages
app.UseRouting();
app.UseAuthentication();

app.UseAuthorization();
app.UseSession();
app.MapRazorPages();
app.MapMichuLocalizationEndpoints();

// Referral links (customer portal, "Refer a friend"): /r/MH-7K3Q9P keeps the code for 30 days and opens registration.
app.MapGet("/r/{code}", async (string code, HttpContext http, RadiantHomes.Services.CustomerPortal.ReferralService referrals) =>
{
    if (http.User.Identity?.IsAuthenticated == true) return Results.LocalRedirect("/Portal");
    var referral = await referrals.FindActiveAsync(code);
    if (referral is null) return Results.LocalRedirect("/");
    http.Response.Cookies.Append(RadiantHomes.Services.CustomerPortal.ReferralService.CookieName, referral.Code, new CookieOptions
    {
        Expires = DateTimeOffset.UtcNow.AddDays(30),
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = http.Request.IsHttps,
    });
    return Results.LocalRedirect("/Identity/Account/Register?ref=" + Uri.EscapeDataString(referral.Code));
});

app.Run();
