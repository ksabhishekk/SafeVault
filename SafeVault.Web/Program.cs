using SafeVault.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
    });
builder.Services.AddAuthorization();
var connectionString = builder.Configuration.GetConnectionString("SafeVault")
    ?? throw new InvalidOperationException("The SafeVault connection string is not configured.");
builder.Services.AddSingleton(new UserRepository(connectionString));

var app = builder.Build();

var schemaPath = Path.Combine(AppContext.BaseDirectory, "database.sql");
app.Services.GetRequiredService<UserRepository>()
    .InitializeDatabase(File.ReadAllText(schemaPath));

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program { }
