using MySqlConnector;

namespace Agrumy.Api.Utils
{
    /// Tests connectivity to an admin-supplied archive database BEFORE ServerConfigApiController.Update ever saves the credentials - distinguishes "can't reach the host" from "wrong username/password" from "database doesn't exist", since each needs a different fix and a generic "connection failed" leaves the admin guessing.
    public static class ArchiveDbConnectionTester
    {
        public static async Task<(bool Success, string? Error)> TestAsync(string host, int port, string database, string username, string password, CancellationToken ct = default)
        {
            string connectionString = new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = (uint)port,
                Database = database,
                UserID = username,
                Password = password,
                SslMode = MySqlSslMode.Preferred,
                ConnectionTimeout = 5,
            }.ConnectionString;

            await using var connection = new MySqlConnection(connectionString);
            try
            {
                await connection.OpenAsync(ct);
                return (true, null);
            }
            catch (MySqlException ex) when (ex.Number == 1045) // Access denied for user
            {
                return (false, "Could not authenticate - check the username and password.");
            }
            catch (MySqlException ex) when (ex.Number == 1049) // Unknown database
            {
                return (false, $"Database \"{database}\" does not exist on that server.");
            }
            catch (Exception ex)
            {
                return (false, $"Could not connect to the database server - check host, port, and that it accepts connections from this machine. ({ex.Message})");
            }
        }
    }
}
