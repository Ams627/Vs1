using EnvDTE80;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

record VsInstanceInfo(DTE2 VsDte, int pid);

internal class Program
{
    static void Main()
    {
        if (ShellUtils.ShouldUseForwardSlashes())
        {
            Console.WriteLine("BASH!");
        }
        foreach (var vsInfo in GetRunningVisualStudios())
        {
            try
            {
                string solutionName = vsInfo.VsDte.Solution?.FullName;
                var sln = string.IsNullOrEmpty(solutionName) ? "No solution loaded" : $"{solutionName}";
                var branch = GetGitBranchForSolution(vsInfo.VsDte);

                Console.WriteLine($"{vsInfo.pid,-7}{GetHyperlinkAnsiWithTooltip(sln, "http://ibm.com", "Click me")} {branch}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error accessing DTE: " + ex.Message);
            }

        }
    }

    static string GetHyperlinkAnsiWithTooltip(string text, string url, string tooltip = "", string style = "1;34;4")
    {
        // If tooltip is provided, format the link with it as the URI label
        string encodedTooltip = string.IsNullOrEmpty(tooltip) ? url : $"{url}#{tooltip}";
        return $"\u001b]8;label={tooltip};{url}\u0007\u001b[{style}m{text}\u001b[0m\u001b]8;;\u0007";
    }

    private static string GetHyperlinkAnsi(string text, string url, string colour = "1;34")
    {
        return $"\u001b]8;;{url}\u0007\u001b[{colour}m{text}\u001b[0m\u001b]8;;\u0007";
    }

    static VsInstanceInfo[] GetRunningVisualStudios()
    {
        List<VsInstanceInfo> list = [];
        GetRunningObjectTable(0, out IRunningObjectTable rot);

        rot.EnumRunning(out IEnumMoniker monikerEnumerator);
        IMoniker[] monikers = new IMoniker[1];
        IntPtr fetched = IntPtr.Zero;

        while (monikerEnumerator.Next(1, monikers, fetched) == 0)
        {
            CreateBindCtx(0, out IBindCtx ctx);

            monikers[0].GetDisplayName(ctx, null, out string name);

            // Filter for Visual Studio DTE objects
            if (name.StartsWith("!VisualStudio.DTE"))
            {
                rot.GetObject(monikers[0], out object comObject);
                if (comObject is DTE2 dte)
                {
                    var pidStr = name.Substring(name.LastIndexOf(':') + 1);
                    if (int.TryParse(pidStr, out int pid))
                    {
                        list.Add(new(dte, pid));
                    }
                    else
                    {
                        list.Add(new(dte, 0));
                    }
                }
            }
        }

        return list.ToArray();
    }

    private static string GetGitBranchForSolution(DTE2 dte)
    {
        try
        {
            var solutionPath = dte?.Solution?.FullName;
            if (string.IsNullOrWhiteSpace(solutionPath))
                return null;

            var dir = Path.GetDirectoryName(solutionPath);
            while (dir != null)
            {
                var gitDir = Path.Combine(dir, ".git");

                // Handle standard .git folder
                if (Directory.Exists(gitDir))
                {
                    var headFile = Path.Combine(gitDir, "HEAD");
                    if (File.Exists(headFile))
                    {
                        var headContent = File.ReadAllText(headFile).Trim();
                        if (headContent.StartsWith("ref:"))
                        {
                            return headContent.Split('/').Last(); // Get last part (branch name)
                        }
                        else
                        {
                            return "(detached HEAD)";
                        }
                    }
                }

                // Handle submodule-style ".git" file pointing to real git dir
                if (File.Exists(gitDir))
                {
                    var content = File.ReadAllText(gitDir).Trim();
                    if (content.StartsWith("gitdir:"))
                    {
                        var realGitPath = content.Substring("gitdir:".Length).Trim();
                        if (!Path.IsPathRooted(realGitPath))
                            realGitPath = Path.GetFullPath(Path.Combine(dir, realGitPath));

                        var headFile = Path.Combine(realGitPath, "HEAD");
                        if (File.Exists(headFile))
                        {
                            var headContent = File.ReadAllText(headFile).Trim();
                            if (headContent.StartsWith("ref:"))
                            {
                                return headContent.Split('/').Last();
                            }
                            else
                            {
                                return "(detached HEAD)";
                            }
                        }
                    }
                }

                dir = Directory.GetParent(dir)?.FullName;
            }

            return null;
        }
        catch (Exception ex)
        {
            return $"[error: {ex.Message}]";
        }
    }

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(int reserved, out IBindCtx ppbc);

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(int reserved, out IRunningObjectTable pprot);
}