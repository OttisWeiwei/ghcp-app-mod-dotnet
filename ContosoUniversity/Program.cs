using ContosoUniversity.Data;
using ContosoUniversity.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews().AddNewtonsoftJson();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=(LocalDb)\\MSSQLLocalDB;Initial Catalog=ContosoUniversityNoAuthEFCore;Integrated Security=True;MultipleActiveResultSets=True";

builder.Services.AddDbContext<SchoolContext>(options => options.UseSqlServer(connectionString));

// Register Azure Blob Storage Service
var azureBlobEndpoint = builder.Configuration["AzureStorageBlob:Endpoint"];
if (!string.IsNullOrEmpty(azureBlobEndpoint))
{
    builder.Services.AddScoped(sp => new AzureBlobStorageService(azureBlobEndpoint));
}

// Register Notification Service
builder.Services.AddScoped<NotificationService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<SchoolContext>();
    DbInitializer.Initialize(context);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
