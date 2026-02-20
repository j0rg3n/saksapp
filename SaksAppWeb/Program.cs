using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaksAppWeb.Data;
using SaksAppWeb.Models;
using SaksAppWeb.Services;

var builder = WebApplication.CreateBuilder(args);

// Data protection keys persistence
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/dp_keys"));

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, SmtpEmailSender>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<ICaseNumberAllocator, CaseNumberAllocator>();
builder.Services.AddScoped<IPdfSequenceService, PdfSequenceService>();
builder.Services.AddScoped<HtmlCaseImporter>();

builder.Services.AddControllersWithViews();

// Hourly SQLite database backups (see Services/DatabaseBackupService)
builder.Services.AddHostedService<DatabaseBackupService>();

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();