using Microsoft.EntityFrameworkCore;
using CampusReportSystem.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register HttpContextAccessor to resolve the InvalidOperationException
builder.Services.AddHttpContextAccessor();

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    try
    {
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<ApplicationDbContext>();
        
        DbInitializer.Initialize(context);

        string createTableSql = @"
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reactions')
            BEGIN
                CREATE TABLE Reactions (
                    Id INT IDENTITY(1,1) PRIMARY KEY,
                    ReportId INT NOT NULL,
                    UserId INT NOT NULL,
                    CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
                );
            END";
            
        context.Database.ExecuteSqlRaw(createTableSql);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Database Initialization Failed: {ex.Message}");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();