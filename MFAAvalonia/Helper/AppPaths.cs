using System;
using System.IO;
using System.Linq;

namespace MFAAvalonia.Helper;

public static class AppPaths
{
    private const string AppFolderName = "MFAAvalonia";
    private const string MigrationMarkerFileName = ".data_root_initialized";
    private static bool _initialized;

    public static string InstallRoot => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    public static string DataRoot { get; private set; } = InstallRoot;
    public static string ConfigDirectory => Path.Combine(DataRoot, "config");
    public static string InstancesDirectory => Path.Combine(ConfigDirectory, "instances");
    public static string ResourceDirectory => Path.Combine(DataRoot, "resource");
    public static string AgentDirectory => Path.Combine(DataRoot, "agent");
    public static string LogsDirectory => Path.Combine(DataRoot, "logs");
    public static string TempDirectory => Path.Combine(DataRoot, "temp");
    public static string TempResourceDirectory => Path.Combine(TempDirectory, "temp_res");
    public static string TempMfaDirectory => Path.Combine(TempDirectory, "temp_mfa");
    public static string TempMaaFwDirectory => Path.Combine(TempDirectory, "temp_maafw");
    public static string InterfaceJsonPath => Path.Combine(DataRoot, "interface.json");
    public static string InterfaceJsoncPath => Path.Combine(DataRoot, "interface.jsonc");
    public static string GlobalConfigPath => Path.Combine(DataRoot, "appsettings.json");
    public static string ChangesPath => Path.Combine(DataRoot, "changes.json");
    public static string BackupDirectory => Path.Combine(DataRoot, "backup");
    public static bool IsUsingIndependentDataRoot => !string.Equals(DataRoot, InstallRoot, StringComparison.OrdinalIgnoreCase);

    public static void Initialize()
    {
        if (_initialized)
            return;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidateRoot = string.IsNullOrWhiteSpace(localAppData)
            ? InstallRoot
            : Path.Combine(localAppData, AppFolderName);

        DataRoot = candidateRoot;
        Directory.CreateDirectory(DataRoot);

        if (!File.Exists(GetMarkerPath()))
        {
            MigrateLegacyDataIfNeeded();
            File.WriteAllText(GetMarkerPath(), DateTime.UtcNow.ToString("O"));
        }

        Directory.CreateDirectory(ConfigDirectory);
        Directory.CreateDirectory(InstancesDirectory);
        Directory.CreateDirectory(ResourceDirectory);
        Directory.CreateDirectory(AgentDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(BackupDirectory);

        LogInitializationStatus();

        _initialized = true;
    }

    private static string GetMarkerPath() => Path.Combine(DataRoot, MigrationMarkerFileName);

    private static void MigrateLegacyDataIfNeeded()
    {
        if (string.Equals(DataRoot, InstallRoot, StringComparison.OrdinalIgnoreCase))
            return;

        CopyDirectoryIfExists(Path.Combine(InstallRoot, "config"), ConfigDirectory);
        CopyDirectoryIfExists(Path.Combine(InstallRoot, "resource"), ResourceDirectory);
        CopyDirectoryIfExists(Path.Combine(InstallRoot, "agent"), AgentDirectory);
        CopyDirectoryIfExists(Path.Combine(InstallRoot, "logs"), LogsDirectory);

        CopyFileIfExists(Path.Combine(InstallRoot, "interface.json"), InterfaceJsonPath);
        CopyFileIfExists(Path.Combine(InstallRoot, "interface.jsonc"), InterfaceJsoncPath);
        CopyFileIfExists(Path.Combine(InstallRoot, "appsettings.json"), GlobalConfigPath);
        CopyFileIfExists(Path.Combine(InstallRoot, "changes.json"), ChangesPath);
    }

    private static void CopyDirectoryIfExists(string source, string destination)
    {
        if (!Directory.Exists(source))
            return;

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destination, relativePath));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(source, file);
            var targetFile = Path.Combine(destination, relativePath);
            var targetDir = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetDir))
                Directory.CreateDirectory(targetDir);
            if (!File.Exists(targetFile))
                File.Copy(file, targetFile, false);
        }
    }

    private static void CopyFileIfExists(string source, string destination)
    {
        if (!File.Exists(source) || File.Exists(destination))
            return;

        var targetDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(targetDir))
            Directory.CreateDirectory(targetDir);
        File.Copy(source, destination, false);
    }

    private static void LogInitializationStatus()
    {
        try
        {
            LoggerHelper.Info($"应用安装目录：{InstallRoot}");
            LoggerHelper.Info($"应用数据目录：{DataRoot}");
            LoggerHelper.Info($"是否使用独立数据目录：{IsUsingIndependentDataRoot}");

            var backupCount = Directory.Exists(DataRoot)
                ? Directory.EnumerateFiles(DataRoot, "*.backupMFA", SearchOption.AllDirectories).Count()
                : 0;
            var tempDirExists = Directory.Exists(TempDirectory);
            LoggerHelper.Info($"备份文件数量：{backupCount}");
            LoggerHelper.Info($"临时目录是否存在：{tempDirExists}");
        }
        catch
        {
            // Avoid breaking startup for diagnostics.
        }
    }
}
