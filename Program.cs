using Microsoft.EntityFrameworkCore;
using Serilog;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Services.Implementations;
using TutoringCenterManagement.Services.Interfaces;
using TutoringCenterManagement.Services.Background;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// CẤU HÌNH SERVICES
// =============================================

builder.Services.AddScoped<IUserImportExportService, UserImportExportService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    ));

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddScoped<UserImportExportService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddHostedService<SessionStatusUpdater>();
// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".TutoringCenter.Session";
});

builder.Services.AddScoped<IScheduleService, ScheduleService>();

// Razor Pages
builder.Services.AddRazorPages();

var app = builder.Build();

// =============================================
// MIDDLEWARE PIPELINE
// =============================================

// Database Initialization
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Tự động apply các migration mới nhất, không xóa data
        context.Database.Migrate();
        Log.Information("Database migrated successfully");

        // Seed data (DbInitializer nên tự kiểm tra trước khi insert để tránh trùng)
        DbInitializer.Initialize(context);
        Log.Information("Seed data completed");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "An error occurred during database initialization");
        throw;
    }
}

// Error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapRazorPages();

// =============================================
// RUN
// =============================================

try
{
    Log.Information("Starting Tutoring Center Management System");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}