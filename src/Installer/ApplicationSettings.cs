namespace Installer;

public static class ApplicationSettings
{
    public static string AppName => "aws-ssm-cli";

    public static string Shortcut => "ascli";

    public static string ProjectPath => "src/Aws.Ssm.Cli/Aws.Ssm.Cli.csproj";

    public static Dictionary<string, Func<string>> DefaultArguments => 
        new()
        {
            ["BuildWorkingDirectory"] = () => "../",
            ["OsxAppPath"] = () => string.Format($"/opt/{AppName}"),
            ["OsxPathsD"] = () => string.Format($"/etc/paths.d/{AppName}"),
            ["OsxShortcut"] = () => Shortcut,
            ["WinAppPath"] = () =>
            {
                var path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                return Path.Combine(path, AppName);
            },
        };
}