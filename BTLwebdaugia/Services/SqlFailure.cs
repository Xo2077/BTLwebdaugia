using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BTLwebdaugia.Services;

public static class SqlFailure
{
    public static bool IsConcurrencyConflict(Exception exception)
    {
        var sql = exception as SqlException ??
            (exception as DbUpdateException)?.InnerException as SqlException;
        return sql?.Number is 1205 or 1222 or 3960;
    }
}
