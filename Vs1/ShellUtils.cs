using System;
using System.Diagnostics;
using System.Linq;
using System.Management;

public static class ShellUtils
{
    public static string Bashify(string windowsPath)
    {
        if (string.IsNullOrWhiteSpace(windowsPath))
            return string.Empty;

        string normalized = windowsPath.Replace('\\', '/');

        // Match "C:" or any drive letter at the beginning
        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            char driveLetter = char.ToLower(normalized[0]);
            normalized = "/" + driveLetter + normalized.Substring(2);
        }

        return normalized;
    }
    public static string GetFirstShellAncestorName()
    {
        var knownShells = new[] { "bash", "cmd", "powershell", "pwsh", "sh", "mintty", "zsh" };
        int pid = Process.GetCurrentProcess().Id;
        int maxDepth = 20;

        for (int i = 0; i < maxDepth; i++)
        {
            var parent = GetParentProcess(pid);
            if (parent == null)
                break;

            var name = parent.ProcessName.ToLowerInvariant();

            if (knownShells.Contains(name))
                return name;

            pid = parent.Id;
        }

        return null; // No known shell found
    }

    public static bool ShouldUseForwardSlashes()
    {
        string shell = GetFirstShellAncestorName();
        return shell == "bash" || shell == "sh" || shell == "zsh" || shell == "mintty";
    }

    private static Process GetParentProcess(int pid)
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher(
                $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {pid}"))
            {
                var result = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
                if (result != null)
                {
                    int ppid = Convert.ToInt32(result["ParentProcessId"]);
                    return Process.GetProcessById(ppid);
                }
            }
        }
        catch
        {
            // Swallow and fail gracefully
        }

        return null;
    }
}
