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
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login";
        options.LogoutPath = "/Identity/Account/Logout";
    })
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
    }).AddFacebook(facebookOptions =>
    {
        facebookOptions.AppId = builder.Configuration["Authentication:Facebook:AppId"];
        facebookOptions.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
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
        path: "Logs/log-.txt",
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({UserName}) {SourceContext} {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog(); // Plug into the host

builder.Services.AddSingleton<IHttpContextAccessor>(httpContextAccessor); // register it for DI too



builder.Host.UseSerilog(); // Plug into the host

var app = builder.Build();
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error").UseAntiforgery();
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
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
