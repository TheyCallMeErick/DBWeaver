using DBWeaver.UI.Services.Explain;
using System.Diagnostics;

namespace DBWeaver.Tests.Integration.Explain;

internal static class DockerImageAvailability
{
    public static bool IsPresentLocally(string image)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("image");
        psi.ArgumentList.Add("inspect");
        psi.ArgumentList.Add(image);

        using var process = new Process { StartInfo = psi };
        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }
}

