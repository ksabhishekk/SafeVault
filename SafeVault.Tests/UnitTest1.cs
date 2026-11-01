using System.Net;
using Microsoft.Data.Sqlite;
using SafeVault.Web.Data;

namespace SafeVault.Tests;

public sealed class InputAndRepositoryTests
{
    private SqliteConnection _databaseKeeper = null!;
    private UserRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        var connectionString = $"Data Source=SafeVaultTests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        _databaseKeeper = new SqliteConnection(connectionString);
        _databaseKeeper.Open();
        _repository = new UserRepository(connectionString);

        var schemaPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "database.sql");
        _repository.InitializeDatabase(File.ReadAllText(schemaPath));
        _repository.InsertUser("admin_user", "admin@example.com", "hashed-password");
    }

    [TearDown]
    public void TearDown()
    {
        _databaseKeeper.Dispose();
    }

    [TestCase("admin' OR '1'='1")]
    [TestCase("'; DROP TABLE Users;--")]
    public void TestForSQLInjection(string payload)
    {
        Assert.That(InputValidator.IsValidUsername(payload), Is.False);
        Assert.That(_repository.GetByUsername(payload), Is.Null);

        using var command = _databaseKeeper.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Users';";
        Assert.That(command.ExecuteScalar(), Is.EqualTo(1L));
    }

    [TestCase("<script>alert('xss')</script>")]
    [TestCase("<img src=x onerror=alert(1)>")]
    public void TestForXSS(string payload)
    {
        Assert.That(InputValidator.IsValidUsername(payload), Is.False);
        Assert.That(InputValidator.IsValidEmail(payload), Is.False);

        var encodedOutput = WebUtility.HtmlEncode(payload);
        Assert.That(encodedOutput, Does.Not.Contain("<"));
    }

    [Test]
    public void ValidUserCanBeInsertedAndRetrievedByUsername()
    {
        var user = _repository.GetByUsername("admin_user");

        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Email, Is.EqualTo("admin@example.com"));
        Assert.That(user.PasswordHash, Is.EqualTo("hashed-password"));
        Assert.That(user.Role, Is.EqualTo("user"));
    }

    [TestCase("abc")]
    [TestCase("user_123")]
    public void ValidUsernamesAreAccepted(string username)
    {
        Assert.That(InputValidator.IsValidUsername(username), Is.True);
    }

    [TestCase("person@example.com")]
    [TestCase("first.last+tag@example.co.uk")]
    public void ValidEmailsAreAccepted(string email)
    {
        Assert.That(InputValidator.IsValidEmail(email), Is.True);
    }

    [TestCase("first..last@example.com")]
    [TestCase("first.@example.com")]
    public void InvalidEmailsAreRejected(string email)
    {
        Assert.That(InputValidator.IsValidEmail(email), Is.False);
    }
}
