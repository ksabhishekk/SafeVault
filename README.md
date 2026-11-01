# SafeVault

SafeVault is an ASP.NET Core Razor Pages application using SQLite for user storage.

## Run

From the repository root:

```powershell
dotnet run --project SafeVault.Web
```

The app creates `SafeVault.db` using the schema in [`database.sql`](database.sql). The database connection string can be changed in `SafeVault.Web/appsettings.json`.

## Test

From the repository root:

```powershell
dotnet test
```

The tests cover input validation, parameterized SQLite queries, login behavior, antiforgery protection, rate limiting, cookie flags, and admin role access.
