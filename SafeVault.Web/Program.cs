using SafeVault.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}:{context.Request.Method}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = context.Request.Method == HttpMethods.Post ? 5 : 1000,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync(
            "Too many login attempts. Please try again later.",
            cancellationToken);
    };
});
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

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program { }
