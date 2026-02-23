using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>
/// Native file/folder picker dialog for Linux.
/// Uses zenity (GNOME) or kdialog (KDE).
/// </summary>
public static class NativeFilePicker
{
    public enum DialogType { OpenFile, OpenFolder, SaveFile }

    // ─── Sync methods ────────────────────────────────────────────────────

    public static string OpenFolder(string title = "Select Folder", string startPath = null)
        => ShowDialog(DialogType.OpenFolder, title, startPath);

    public static string OpenFile(string title = "Select File", string startPath = null, string filter = null)
        => ShowDialog(DialogType.OpenFile, title, startPath, filter);

    public static string SaveFile(string title = "Save File", string startPath = null, string filter = null)
        => ShowDialog(DialogType.SaveFile, title, startPath, filter);

    // ─── Async versions (non-blocking) ────────────────────────────────

    public static void OpenFolderAsync(Action<string> callback, string title = "Select Folder", string startPath = null)
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            var result = OpenFolder(title, startPath);
            UnityMainThreadDispatcher.Enqueue(() => callback?.Invoke(result));
        });
    }

    public static void OpenFileAsync(Action<string> callback, string title = "Select File",
        string startPath = null, string filter = null)
    {
        System.Threading.Tasks.Task.Run(() =>
        {
            var result = OpenFile(title, startPath, filter);
            UnityMainThreadDispatcher.Enqueue(() => callback?.Invoke(result));
        });
    }

    // ─── Internal logic ────────────────────────────────────────────────────

    private static string ShowDialog(DialogType type, string title, string startPath, string filter = null)
    {
        if (Application.platform != RuntimePlatform.LinuxPlayer &&
            Application.platform != RuntimePlatform.LinuxEditor)
        {
            UnityEngine.Debug.LogWarning("[NativeFilePicker] Linux only.");
            return null;
        }

        // Detect available dialog
        if (IsCommandAvailable("zenity"))
            return RunZenity(type, title, startPath, filter);
        if (IsCommandAvailable("kdialog"))
            return RunKdialog(type, title, startPath, filter);
        if (IsCommandAvailable("yad"))
            return RunYad(type, title, startPath, filter);

        UnityEngine.Debug.LogError(
            "[NativeFilePicker] Neither zenity, kdialog nor yad found.\n" +
            "Install: sudo apt install zenity  or  sudo pacman -S zenity");
        return null;
    }

    // ─── Zenity (GNOME/GTK) ───────────────────────────────────────────────────

    private static string RunZenity(DialogType type, string title, string startPath, string filter)
    {
        var args = type switch
        {
            DialogType.OpenFolder => $"--file-selection --directory --title=\"{title}\"",
            DialogType.SaveFile   => $"--file-selection --save --title=\"{title}\"",
            _                     => $"--file-selection --title=\"{title}\"",
        };

        if (!string.IsNullOrEmpty(startPath))
            args += $" --filename=\"{EnsureTrailingSlash(startPath)}\"";

        if (!string.IsNullOrEmpty(filter) && type != DialogType.OpenFolder)
            args += $" --file-filter=\"{filter}\"";

        return RunProcess("zenity", args);
    }

    // ─── KDialog (KDE/Qt) ─────────────────────────────────────────────────────

    private static string RunKdialog(DialogType type, string title, string startPath, string filter)
    {
        var start = string.IsNullOrEmpty(startPath) ? "" : $"\"{startPath}\"";

        var args = type switch
        {
            DialogType.OpenFolder => $"--getexistingdirectory {start} --title \"{title}\"",
            DialogType.SaveFile   => $"--getsavefilename {start} \"{filter ?? ""}\" --title \"{title}\"",
            _                     => $"--getopenfilename {start} \"{filter ?? ""}\" --title \"{title}\"",
        };

        return RunProcess("kdialog", args);
    }

    // ─── YAD (Yet Another Dialog) ─────────────────────────────────────────────

    private static string RunYad(DialogType type, string title, string startPath, string filter)
    {
        var args = type switch
        {
            DialogType.OpenFolder => $"--file --directory --title=\"{title}\"",
            DialogType.SaveFile   => $"--file --save --title=\"{title}\"",
            _                     => $"--file --title=\"{title}\"",
        };

        if (!string.IsNullOrEmpty(startPath))
            args += $" --filename=\"{EnsureTrailingSlash(startPath)}\"";

        return RunProcess("yad", args);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private static string RunProcess(string command, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = command,
                Arguments              = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using var process = Process.Start(psi);
            var output = process!.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            // Non-zero exit code = user pressed Cancel
            return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[NativeFilePicker] Error launching {command}: {ex.Message}");
            return null;
        }
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = "which",
                Arguments              = command,
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var p = Process.Start(psi);
            var output = p!.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return !string.IsNullOrEmpty(output);
        }
        catch { return false; }
    }

    private static string EnsureTrailingSlash(string path)
        => path.EndsWith("/") ? path : path + "/";
}
