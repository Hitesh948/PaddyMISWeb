using Microsoft.Data.SqlClient;

namespace PaddyMISWeb.Data
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("PaddyTrolleyDB")
                ?? throw new InvalidOperationException(
                    "PaddyTrolleyDB connection string not found.");
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}