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

    /// <summary>
    /// Verifies database reachability by opening a connection and executing
    /// a lightweight <c>SELECT 1</c> ping query.
    ///
    /// <para>Declared <c>virtual</c> so test doubles can override it without
    /// requiring a real MySQL server.</para>
    /// </summary>
    /// <exception cref="Exception">
    /// Any exception (e.g. <see cref="MySqlException"/>) thrown here signals
    /// that the database is unreachable and the caller should report <b>Degraded</b>.
    /// </exception>
    public virtual async Task PingAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var cmd = new MySqlCommand("SELECT 1", connection);
        await cmd.ExecuteScalarAsync();
    }
}
