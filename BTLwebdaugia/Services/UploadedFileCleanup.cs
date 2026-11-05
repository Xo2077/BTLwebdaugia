namespace BTLwebdaugia.Services;

public static class UploadedFileCleanup
{
    public static void Delete(string path, ILogger logger)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Cleanup failure must not hide the original SQL/file operation failure.
            logger.LogWarning(exception, "Unable to remove uploaded file {FilePath}", path);
        }
    }
}
