using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SafeVault.Web.Data;

namespace SafeVault.Tests;

public sealed class AuthenticationIntegrationTests
{
    private const string InvalidCredentialsMessage = "Invalid username or password";
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _databasePath = null!;

    [SetUp]
    public void SetUp()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"SafeVaultAuth-{Guid.NewGuid():N}.db");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<UserRepository>();
                services.AddSingleton(new UserRepository($"Data Source={_databasePath};Pooling=False"));
            }));
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        var repository = _factory.Services.GetRequiredService<UserRepository>();
        repository.InsertUser("normal_user", "normal@example.com", BCrypt.Net.BCrypt.HashPassword("user-password", 4));
        repository.InsertUser("admin_user", "admin@example.com", BCrypt.Net.BCrypt.HashPassword("admin-password", 4), "admin");
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
        File.Delete(_databasePath);
    }

    [Test]
    public async Task WrongPasswordReturnsGenericError()
    {
        var response = await PostLoginAsync("normal_user", "wrong-password");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain(InvalidCredentialsMessage));
    }

    [Test]
    public async Task UnknownUserReturnsSameGenericError()
    {
        var response = await PostLoginAsync("missing_user", "wrong-password");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain(InvalidCredentialsMessage));
    }

    [Test]
    public async Task CorrectPasswordSignsUserIn()
    {
        var response = await PostLoginAsync("normal_user", "user-password");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        var setCookie = string.Join("; ", response.Headers.GetValues("Set-Cookie"));
        Assert.That(setCookie, Does.Contain("httponly"));
        Assert.That(setCookie, Does.Contain("secure"));

        var protectedResponse = await _client.GetAsync("/Admin");
        Assert.That(protectedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
    }

    [Test]
    public async Task NormalUserIsDeniedAdminPage()
    {
        await PostLoginAsync("normal_user", "user-password");

        var response = await _client.GetAsync("/Admin");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        Assert.That(response.Headers.Location?.AbsolutePath, Is.EqualTo("/AccessDenied"));
        var deniedPage = await _client.GetAsync("/AccessDenied");
        Assert.That(deniedPage.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await deniedPage.Content.ReadAsStringAsync(), Does.Contain("Access denied"));
    }

    [Test]
    public async Task AdminCanAccessAdminPage()
    {
        await PostLoginAsync("admin_user", "admin-password");

        var response = await _client.GetAsync("/Admin");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Only administrators can view this page."));
    }

    [Test]
    public async Task LoginRejectsRequestsWithoutAntiforgeryToken()
    {
        var response = await _client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "normal_user",
            ["Input.Password"] = "user-password"
        }));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task LoginRateLimitsRepeatedAttemptsFromSameAddress()
    {
        HttpResponseMessage? lastResponse = null;
        var token = await GetAntiforgeryTokenAsync();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            lastResponse = await _client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Username"] = "normal_user",
                ["Input.Password"] = "wrong-password",
                ["__RequestVerificationToken"] = token
            }));
        }

        Assert.That(lastResponse!.StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(await lastResponse.Content.ReadAsStringAsync(), Does.Contain("Too many login attempts"));
    }

    private async Task<HttpResponseMessage> PostLoginAsync(string username, string password)
    {
        var token = await GetAntiforgeryTokenAsync();
        return await _client.PostAsync("/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = username,
            ["Input.Password"] = password,
            ["__RequestVerificationToken"] = token
        }));
    }

    private async Task<string> GetAntiforgeryTokenAsync()
    {
        var loginPage = await _client.GetAsync("/Login");
        var html = await loginPage.Content.ReadAsStringAsync();
        var token = Regex.Match(
            html,
            @"<input name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""")
            .Groups[1].Value;
        Assert.That(token, Is.Not.Empty, "The login page should provide an antiforgery token.");
        return token;
    }
}
