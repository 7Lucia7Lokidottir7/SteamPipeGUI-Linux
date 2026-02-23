using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Manages Steam depots and generates VDF files for steamcmd.
/// </summary>
public class DepotManager
{
    // ─── Models ───────────────────────────────────────────────────────────────

    [Serializable]
    public class DepotConfig
    {
        public string DepotId       { get; set; }
        public string ContentPath   { get; set; }
        public string LocalPath     { get; set; } = "*";       // what to take from ContentPath
        public string DepotPath     { get; set; } = ".";       // where to put in depot
        public bool   Recursive     { get; set; } = true;
        public string FileExclusion { get; set; } = "";        // e.g. "*.pdb"
    }

    [Serializable]
    public class BuildConfig
    {
        public string AppId         { get; set; }
        public string Description   { get; set; }
        public string ContentRoot   { get; set; }              // base directory
        public string Branch        { get; set; } = "";        // empty = don't set live
        public bool   Preview       { get; set; } = false;     // dry run, don't upload
        public List<DepotConfig> Depots { get; set; } = new();
    }

    // ─── VDF Generation ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an app build VDF file in a temp directory and returns its path.
    /// </summary>
    public static string GenerateAppBuildVdf(BuildConfig config)
    {
        if (string.IsNullOrEmpty(config.AppId))
            throw new ArgumentException("AppId is required.");
        if (config.Depots == null || config.Depots.Count == 0)
            throw new ArgumentException("At least one depot is required.");

        var sb = new StringBuilder();
        sb.AppendLine("\"AppBuild\"");
        sb.AppendLine("{");
        sb.AppendLine($"\t\"AppID\"\t\t\"{config.AppId}\"");
        sb.AppendLine($"\t\"Desc\"\t\t\"{EscapeVdf(config.Description ?? "")}\"");

        if (!string.IsNullOrEmpty(config.ContentRoot))
            sb.AppendLine($"\t\"ContentRoot\"\t\"{config.ContentRoot}\"");

        if (!string.IsNullOrEmpty(config.Branch))
            sb.AppendLine($"\t\"SetLive\"\t\"{config.Branch}\"");

        if (config.Preview)
            sb.AppendLine("\t\"Preview\"\t\"1\"");

        sb.AppendLine("\t\"Depots\"");
        sb.AppendLine("\t{");

        foreach (var depot in config.Depots)
            AppendDepotVdf(sb, depot);

        sb.AppendLine("\t}");
        sb.AppendLine("}");

        // Save to temp folder
        var vdfDir  = Path.Combine(Path.GetTempPath(), "SteamPipeGUI");
        Directory.CreateDirectory(vdfDir);
        var vdfPath = Path.Combine(vdfDir, $"app_{config.AppId}_build.vdf");
        File.WriteAllText(vdfPath, sb.ToString(), Encoding.UTF8);

        Debug.Log($"[DepotManager] VDF created: {vdfPath}");
        return vdfPath;
    }

    private static void AppendDepotVdf(StringBuilder sb, DepotConfig depot)
    {
        sb.AppendLine($"\t\t\"{depot.DepotId}\"");
        sb.AppendLine("\t\t{");
        sb.AppendLine("\t\t\t\"FileMapping\"");
        sb.AppendLine("\t\t\t{");
        sb.AppendLine($"\t\t\t\t\"LocalPath\"\t\"{depot.LocalPath}\"");
        sb.AppendLine($"\t\t\t\t\"DepotPath\"\t\"{depot.DepotPath}\"");
        sb.AppendLine($"\t\t\t\t\"recursive\"\t\"{(depot.Recursive ? "1" : "0")}\"");
        sb.AppendLine("\t\t\t}");

        if (!string.IsNullOrEmpty(depot.FileExclusion))
        {
            sb.AppendLine("\t\t\t\"FileExclusion\"");
            sb.AppendLine("\t\t\t{");
            sb.AppendLine($"\t\t\t\t\"Pattern\"\t\"{depot.FileExclusion}\"");
            sb.AppendLine("\t\t\t}");
        }

        sb.AppendLine("\t\t}");
    }

    /// <summary>
    /// Generates a depot build VDF for a single depot (used for manual builds).
    /// </summary>
    public static string GenerateDepotBuildVdf(DepotConfig depot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("\"DepotBuildConfig\"");
        sb.AppendLine("{");
        sb.AppendLine($"\t\"DepotID\"\t\"{depot.DepotId}\"");
        sb.AppendLine("\t\"FileMapping\"");
        sb.AppendLine("\t{");
        sb.AppendLine($"\t\t\"LocalPath\"\t\"{depot.LocalPath}\"");
        sb.AppendLine($"\t\t\"DepotPath\"\t\"{depot.DepotPath}\"");
        sb.AppendLine($"\t\t\"recursive\"\t\"{(depot.Recursive ? "1" : "0")}\"");
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        var vdfDir  = Path.Combine(Path.GetTempPath(), "SteamPipeGUI");
        Directory.CreateDirectory(vdfDir);
        var vdfPath = Path.Combine(vdfDir, $"depot_{depot.DepotId}.vdf");
        File.WriteAllText(vdfPath, sb.ToString(), Encoding.UTF8);

        return vdfPath;
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static string EscapeVdf(string value)
        => value.Replace("\"", "\\\"").Replace("\\", "\\\\");

    /// <summary>
    /// Reads an existing VDF file and returns its contents (for debugging).
    /// </summary>
    public static string ReadVdfFile(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[DepotManager] VDF not found: {path}");
            return null;
        }
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Creates a minimal config for a single depot from AppID and content path.
    /// Useful for quick setup.
    /// </summary>
    public static BuildConfig CreateSimpleBuildConfig(
        string appId, string contentPath, string description = "", string branch = "")
    {
        // Depot ID for a single app is usually AppID + 1
        var depotId = (long.TryParse(appId, out var id) ? id + 1 : 0).ToString();

        return new BuildConfig
        {
            AppId       = appId,
            Description = description,
            ContentRoot = contentPath,
            Branch      = branch,
            Depots      = new List<DepotConfig>
            {
                new DepotConfig
                {
                    DepotId     = depotId,
                    ContentPath = contentPath,
                    LocalPath   = "*",
                    DepotPath   = ".",
                    Recursive   = true,
                }
            }
        };
    }
}
