using Microsoft.Data.Sqlite;

namespace SafeVault.Web.Data;

public sealed class UserRepository(string connectionString)
{
    public void InitializeDatabase(string schema)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = schema;
        command.ExecuteNonQuery();
    }

    public long InsertUser(string username, string email, string? passwordHash, string role = "user")
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Users (Username, Email, PasswordHash, Role)
            VALUES (@username, @email, @passwordHash, @role);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@email", email);
        command.Parameters.AddWithValue("@passwordHash", (object?)passwordHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@role", role);

        return (long)command.ExecuteScalar()!;
    }

    public User? GetByUsername(string username)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UserID, Username, Email, PasswordHash, Role
            FROM Users
            WHERE Username = @username;
            """;
        command.Parameters.AddWithValue("@username", username);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new User(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4));
    }
}

public sealed record User(long UserID, string Username, string Email, string? PasswordHash, string Role);
