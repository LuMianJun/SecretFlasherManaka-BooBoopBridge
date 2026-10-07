using System;
using System.IO;
using System.Threading.Tasks;
using BooBoopBridge;

namespace Bridge.MockTests;

internal static partial class Program
{
    private static Task DefaultInstallationPathsAsync()
    {
        var options = new BooBoopOptions();
        Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Boo Boop", "Boo Boop.exe"), options.GetHostExecutablePath(), "System Program Files default");
        Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BooBoop", "product_list.json"), options.GetProductCachePath(), "Current-user cache default");
        return Task.CompletedTask;
    }

    private static Task CustomInstallationPathsAsync()
    {
        if (!OperatingSystem.IsWindows()) return Task.CompletedTask;
        var options = new BooBoopOptions
        {
            HostExecutablePath = @"Z:\中文目录\Boo Boop\Boo Boop.exe",
            ProductCachePath = @"Y:\用户缓存\设备数据.json"
        };
        Equal(@"Z:\中文目录\Boo Boop\Boo Boop.exe", options.GetHostExecutablePath(), "Non-C drive with spaces and Unicode");
        Equal(@"Y:\用户缓存\设备数据.json", options.GetProductCachePath(), "Independent cache override");
        Equal(@"Z:\中文目录\Boo Boop", options.GetHostWorkingDirectory(), "Working directory derives from executable");
        return Task.CompletedTask;
    }

    private static Task EnvironmentInstallationPathsAsync()
    {
        const string variable = "BOOBOOP_TEST_INSTALL_ROOT";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, Path.GetTempPath());
            var options = new BooBoopOptions
            {
                HostExecutablePath = Path.Combine("%" + variable + "%", "Boo Boop.exe"),
                ProductCachePath = Path.Combine("%" + variable + "%", "product_list.json")
            };
            Equal(Path.Combine(Path.GetTempPath(), "Boo Boop.exe"), options.GetHostExecutablePath(), "Expanded host variable");
            Equal(Path.Combine(Path.GetTempPath(), "product_list.json"), options.GetProductCachePath(), "Expanded cache variable");
        }
        finally { Environment.SetEnvironmentVariable(variable, previous); }
        return Task.CompletedTask;
    }

    private static Task InvalidInstallationPathsAsync()
    {
        foreach (string path in new[] { "Boo Boop.exe", "./Boo Boop.exe", "%BOOBOOP_TEST_UNDEFINED_PATH%/Boo Boop.exe" })
            Throws<ArgumentException>(() => new BooBoopOptions { HostExecutablePath = path }.GetHostExecutablePath(), "Reject relative or unresolved paths");
        Throws<ArgumentException>(() => new BooBoopOptions { ProductCachePath = "product_list.json" }.GetProductCachePath(), "Reject relative cache path");
        Throws<ArgumentException>(() => new BooBoopOptions { HostExecutablePath = Path.Combine(Path.GetTempPath(), "host.cmd") }.GetHostExecutablePath(), "Reject non-exe host");
        Throws<ArgumentException>(() => new BooBoopOptions { ProductCachePath = Path.GetTempPath() }.GetProductCachePath(), "Reject directory cache path");
        return Task.CompletedTask;
    }

    private static Task NormalizedInstallationPathsAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "booboop-path-test");
        var options = new BooBoopOptions
        {
            HostExecutablePath = "  " + Path.Combine(directory, "nested", "..", "HOST.EXE") + "  ",
            ProductCachePath = " "
        };
        Equal(Path.Combine(directory, "HOST.EXE"), options.GetHostExecutablePath(), "Trim and normalize without requiring a real file");
        Equal(directory, options.GetHostWorkingDirectory(), "Normalized working directory");
        Equal(new BooBoopOptions().GetProductCachePath(), options.GetProductCachePath(), "Whitespace uses default");
        return Task.CompletedTask;
    }
}
