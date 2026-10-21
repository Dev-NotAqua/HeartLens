namespace Hearthlens.Core;

public static class AppPaths
{
    public static string Root { get; } = Ensure(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hearthlens"));

    public static string Cache { get; } = Ensure(Path.Combine(Root, "cache"));

    public static string Images { get; } = Ensure(Path.Combine(Cache, "images"));

    public static string Settings => Path.Combine(Root, "settings.json");

    public static string Database => Path.Combine(Root, "history.db");

    public static string LogFile => Path.Combine(Root, "hearthlens.log");

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}

/// <summary>Tiny append-only diagnostic log.</summary>
public static class Diag
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                var file = new FileInfo(AppPaths.LogFile);
                if (file.Exists && file.Length > 4 * 1024 * 1024)
                {
                    file.Delete();
                }

                File.AppendAllText(AppPaths.LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
        }
    }
}
