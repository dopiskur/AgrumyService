using System.Text.RegularExpressions;
using MySqlConnector;
using Npgsql;

namespace Agrumy.Api.Dal
{
    /// Builds a consistent { reason, message } response body for database failures instead of a bare false / raw exception message.
    public static class DbErrorResponse
    {
        public static object For(DbFailureKind kind) => kind switch
        {
            DbFailureKind.SchemaMissing => new
            {
                reason = "schema_missing",
                message = "The database schema is not provisioned. Restart the service to auto-provision it, or contact the administrator."
            },
            DbFailureKind.ConstraintViolation => new
            {
                reason = "constraint_violation",
                message = "The request conflicts with an existing record or a referenced record does not exist."
            },
            DbFailureKind.InvalidInput => new
            {
                reason = "invalid_input",
                message = "One of the submitted values is too long or the wrong type for its field."
            },
            DbFailureKind.Contention => new
            {
                reason = "contention",
                message = "The database is busy (deadlock or lock timeout). Please try again."
            },
            DbFailureKind.Unknown => new
            {
                reason = "server_error",
                message = "The service hit an unexpected error handling the request."
            },
            _ => new
            {
                reason = "connection_failure",
                message = "The service could not reach the database. Please try again later."
            }
        };

        /// HTTP status for a failure kind: 409 for a constraint violation, 400 for invalid input, 500 for an unknown/unexpected error, otherwise 503.
        public static int StatusCodeFor(DbFailureKind kind) => kind switch
        {
            DbFailureKind.ConstraintViolation => 409,
            DbFailureKind.InvalidInput => 400,
            DbFailureKind.Unknown => 500,
            _ => 503
        };

        /// True if <paramref name="needle"/> appears in <paramref name="ex"/> or any inner exception - EF's DbException/DbUpdateException wrapping usually puts the useful text on an inner exception.
        public static bool Mentions(Exception? ex, string needle)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e.Message.Contains(needle, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// Same intent as <see cref="Mentions"/> but gated on the driver's own error code first, so this can't be fooled by an unrelated exception whose message happens to contain the name - Postgres exposes the parsed constraint name directly (ConstraintName), MySqlConnector only via message text so that part is parsed once the 1062 code has already confirmed it really is a duplicate key; falls back to the plain substring search for anything that isn't a recognized driver exception (e.g. the crafted messages DbExceptionFilterTests uses to avoid needing a live DB).
        public static bool MentionsConstraint(Exception? ex, string constraintName)
        {
            for (Exception? e = ex; e != null; e = e.InnerException)
            {
                if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
                {
                    return string.Equals(pg.ConstraintName, constraintName, System.StringComparison.OrdinalIgnoreCase);
                }
                if (e is MySqlException { Number: 1062 } mysql)
                {
                    return string.Equals(MySqlDuplicateKeyName(mysql.Message), constraintName, System.StringComparison.OrdinalIgnoreCase);
                }
            }
            return Mentions(ex, constraintName);
        }

        /// Pulls the index name out of MySQL's "Duplicate entry '...' for key 'table.index'" (or just 'index' with no table prefix) - the driver never parses this into a property of its own.
        private static string? MySqlDuplicateKeyName(string message)
        {
            var match = Regex.Match(message, @"for key '(?:[^'.]+\.)?([^'.]+)'");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
