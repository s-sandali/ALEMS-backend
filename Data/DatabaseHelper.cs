using MySql.Data.MySqlClient;

namespace backend.Data;

/// <summary>
/// Provides a managed MySQL connection using ADO.NET (MySql.Data).
/// Registered as a Scoped service in DI — one instance per HTTP request.
/// The connection string is read from IConfiguration so that:
///   - Locally: appsettings.json  →  ConnectionStrings:DefaultConnection
///   - Azure:   Environment var   →  ConnectionStrings__DefaultConnection
/// </summary>
public class DatabaseHelper
{
    private readonly string _connectionString;

    public DatabaseHelper(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found. " +
                "Ensure it is set in appsettings.json or as the environment variable " +
                "'ConnectionStrings__DefaultConnection' in Azure App Service.");
    }

    /// <summary>
    /// Creates and opens a new <see cref="MySqlConnection"/>.
    /// The caller is responsible for disposing it (use 'await using').
    /// </summary>
    /// <returns>An open <see cref="MySqlConnection"/>.</returns>
    /// <exception cref="MySqlException">Thrown when the connection cannot be opened.</exception>
    public async Task<MySqlConnection> OpenConnectionAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
