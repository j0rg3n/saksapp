using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Services;

FontResolverBootstrap.Initialize();

var builder = WebApplication.CreateBuilder(args);

// Add logging
builder.Services.AddLogging();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/dp_keys"));

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

// Behind the Caddy proxy the app sees plain HTTP, so it needs the forwarded headers to know the
// request actually arrived over HTTPS — otherwise redirects and secure cookies get it wrong.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // The proxy's address on the compose network is not fixed, so it cannot be pinned here.
    // Trusting any upstream is only acceptable because the app publishes no public port: the
    // proxy is the sole route in. Pin KnownProxies if that ever stops being true.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

if (!builder.Environment.IsDevelopment())
{
    // Never send the auth cookie over plain HTTP in production. Left at the default
    // (SameAsRequest) it would travel unencrypted on any HTTP request that slipped through.
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.HttpOnly = true;
    });

    // ConfigureApplicationCookie only covers the Identity cookie. The antiforgery and temp-data
    // cookies are issued separately and default to SameAsRequest, so they need marking too.
    builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
    builder.Services.Configure<CookieTempDataProviderOptions>(
        options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
}

builder.Services.AddScoped<UserManager<ApplicationUser>, AppUserManager>();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ICaseNumberAllocator, CaseNumberAllocator>();
builder.Services.AddScoped<IPdfSequenceService, PdfSequenceService>();
builder.Services.AddScoped<IHtmlCaseImporter, HtmlCaseImporter>();
builder.Services.AddScoped<IUserDisplayService, UserDisplayService>();
builder.Services.AddScoped<ICaseQueryService, CaseQueryService>();
builder.Services.AddScoped<IMeetingQueryService, MeetingQueryService>();
builder.Services.AddSingleton<IAppTimeZoneProvider, AppTimeZoneProvider>();
builder.Services.AddSingleton<ISimplePdfWriterFactory, SimplePdfWriterFactory>();
builder.Services.AddScoped<IAgendaPdfDataService, AgendaPdfDataService>();
builder.Services.AddScoped<IMinutesPdfDataService, MinutesPdfDataService>();
builder.Services.AddScoped<IMinutesSaveService, MinutesSaveService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Required by Identity UI (AddIdentity doesn't add this, AddDefaultIdentity does)

// Hourly SQLite database backups (see Services/DatabaseBackupService)
builder.Services.AddSingleton<IDatabaseBackupExecutor, DatabaseBackupExecutor>();
builder.Services.AddHostedService<DatabaseBackupService>();

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// Must run before anything that inspects the scheme or the client address.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.UseMiddleware<RequireApprovedUserMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

public partial class Program { }