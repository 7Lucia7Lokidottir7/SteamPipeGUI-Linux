using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class SteamCmdWrapper
{
    public event Action<string> OnLogOutput;
    public event Action<string> OnStatusChanged;

    public bool IsLoggedIn      { get; private set; }
    public bool IsSteamCmdFound => _steamCmdPath != null;
    public string LoggedInUser  { get; private set; }

    private string _steamCmdPath;

    public SteamCmdWrapper()
    {
        _steamCmdPath = FindSteamCmd();

        if (_steamCmdPath != null)
            Log($"[OK] steamcmd found: {_steamCmdPath}");
        else
        {
            Log("[WARN] steamcmd not found.");
            Log("[INFO] Set the Steamworks SDK folder in Settings.");
        }
    }

    // ─── Find steamcmd ───────────────────────────────────────────────────────

    private string FindSteamCmd()
    {
        // In Linux build BaseDirectory = SteamPipeGUI_Data/Managed/
        // We need the folder next to .x86_64, go up
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var exeDir  = Path.GetFullPath(Combine(baseDir, "..", ".."));  // exit _Data/Managed/

        // 1. Static candidates
        var staticCandidates = new[]
        {
            Combine(exeDir, "builder_linux", "steamcmd.sh"),
            Combine(exeDir, "..", "builder_linux", "steamcmd.sh"),
            Combine(exeDir, "sdk", "tools", "ContentBuilder", "builder_linux", "steamcmd.sh"),
            Combine(exeDir, "..", "sdk", "tools", "ContentBuilder", "builder_linux", "steamcmd.sh"),
            Combine(exeDir, "..", "..", "sdk", "tools", "ContentBuilder", "builder_linux", "steamcmd.sh"),
            Combine(Home(), "sdk", "tools", "ContentBuilder", "builder_linux", "steamcmd.sh"),
            Combine(Home(), "SteamworksSDK", "tools", "ContentBuilder", "builder_linux", "steamcmd.sh"),
            Combine(Home(), "steamworks_sdk", "tools", "ContentBuilder", "builder_linux", "steamcmd.sh"),
            "/usr/bin/steamcmd",
            "/usr/games/steamcmd",
        };

        foreach (var p in staticCandidates)
            if (File.Exists(p)) return p;

        // 2. Glob: find steamworks_sdk_* next to exe (any version)
        foreach (var p in FindSdkByGlob(exeDir))
            if (File.Exists(p)) return p;

        // 3. which
        var which = RunWhich("steamcmd");
        if (which != null) return which;

        return null;
    }

    private static IEnumerable<string> FindSdkByGlob(string baseDir)
    {
        var searchRoots = new[]
        {
            baseDir,
            Path.GetFullPath(Combine(baseDir, "..")),
            Path.GetFullPath(Combine(baseDir, "..", "..")),
            Path.GetFullPath(Combine(baseDir, "..", "..", "..")),
        };

        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root)) continue;
            foreach (var dir in Directory.GetDirectories(root, "steamworks_sdk*"))
                yield return Combine(dir, "sdk", "tools", "ContentBuilder", "builder_linux", "steamcmd.sh");
        }
    }

    public bool TrySetSdkFolder(string sdkFolder)
    {
        var candidates = new[]
        {
            Combine(sdkFolder, "tools", "ContentBuilder", "builder_linux", "steamcmd.sh"),
            Combine(sdkFolder, "builder_linux", "steamcmd.sh"),
            Combine(sdkFolder, "steamcmd.sh"),
        };

        foreach (var p in candidates)
        {
            if (!File.Exists(p)) continue;
            _steamCmdPath = p;
            Log($"[OK] steamcmd: {_steamCmdPath}");
            return true;
        }

        Log($"[ERROR] steamcmd.sh not found in: {sdkFolder}");
        Log("[INFO]  Expected: <sdk>/tools/ContentBuilder/builder_linux/steamcmd.sh");
        return false;
    }

    public void SetSteamCmdPath(string path)
    {
        if (File.Exists(path))
        {
            _steamCmdPath = path;
            Log($"[OK] steamcmd path: {path}");
        }
        else
        {
            Log($"[ERROR] File not found: {path}");
        }
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    public async Task LoginAsync(string username, string password, string guardCode = "")
    {
        Status("Logging in...");

        var args = $"+login {Quote(username)} {Quote(password)}";
        if (!string.IsNullOrEmpty(guardCode))
            args += $" {guardCode.Trim()}";
        args += " +quit";

        var output = await RunAsync(args);

        // steamcmd signals success via "Unloading Steam API" + exit code 0
        // "Logged in OK" only appears on repeat login with cached credentials
        bool success = output.Contains("Logged in OK")
                    || output.Contains("Login Successful")
                    || output.Contains("Unloading Steam API");

        bool wrongGuard  = output.Contains("Two-factor code mismatch")
                        || output.Contains("Invalid Steam Guard")
                        || output.Contains("Invalid authenticator code");
        bool wrongPass   = output.Contains("Invalid Password")
                        || output.Contains("FAILED login");
        bool rateLimited = output.Contains("Too many login failures");

        if (success && !wrongGuard && !wrongPass)
        {
            IsLoggedIn   = true;
            LoggedInUser = username;
            Status($"✓ {username}");
            Log("[OK] Login successful.");
        }
        else if (wrongGuard)
        {
            Status("Steam Guard error");
            Log("[ERROR] Invalid Steam Guard code.");
        }
        else if (wrongPass)
        {
            Status("Login failed");
            Log("[ERROR] Invalid username or password.");
        }
        else if (rateLimited)
        {
            Status("Too many attempts");
            Log("[ERROR] Steam temporarily blocked login. Wait a few minutes.");
        }
        else
        {
            Status("Login failed");
            Log("[WARN] Unexpected steamcmd response. Check the log above.");
        }
    }

    public void Logout()
    {
        IsLoggedIn   = false;
        LoggedInUser = null;
        Status("Not connected");
        Log("[INFO] Logged out.");
    }

    // ─── Build & Upload ───────────────────────────────────────────────────────

    public async Task BuildAndUploadAsync(DepotManager.BuildConfig buildConfig)
    {
        if (!IsLoggedIn)
        {
            Log("[ERROR] Please log in first.");
            return;
        }

        Status("Generating VDF...");
        string vdfPath;
        try
        {
            vdfPath = DepotManager.GenerateAppBuildVdf(buildConfig);
            Log($"[INFO] VDF created: {vdfPath}");
        }
        catch (Exception ex)
        {
            Log($"[ERROR] VDF generation error: {ex.Message}");
            Status("Error");
            return;
        }

        Status("Uploading...");
        var args = $"+login {Quote(LoggedInUser)} +run_app_build {Quote(vdfPath)} +quit";
        var output = await RunAsync(args);

        if (output.Contains("Building depot") || output.Contains("Uploading content"))
            Log("[OK] Upload complete.");
        else if (output.Contains("ERROR"))
            Log("[ERROR] steamcmd returned an error. Check the log above.");

        Status(IsLoggedIn ? $"✓ {LoggedInUser}" : "Ready");
    }

    public Task BuildAndUploadAsync(
        string appId, string description, string contentPath,
        string branch, bool setLive)
    {
        var config = DepotManager.CreateSimpleBuildConfig(
            appId, contentPath, description, setLive ? branch : "");
        return BuildAndUploadAsync(config);
    }

    public async Task<string> RunCommandAsync(string steamCmdArgs)
        => await RunAsync(steamCmdArgs);

    // ─── Private helpers ────────────────────────────────────────────────────

    private async Task<string> RunAsync(string arguments)
    {
        if (_steamCmdPath == null)
        {
            Log("[ERROR] steamcmd not found. Set the Steamworks SDK folder in Settings.");
            return string.Empty;
        }

        var output = "";

        await Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName               = _steamCmdPath,
                Arguments              = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
                WorkingDirectory       = Path.GetDirectoryName(_steamCmdPath),
            };
            psi.EnvironmentVariables["TERM"] = "xterm";

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                output += e.Data + "\n";
                Log(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                Log($"[STDERR] {e.Data}");
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            Log($"[INFO] steamcmd exited with code {process.ExitCode}");
        });

        return output;
    }

    private static string RunWhich(string command)
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
            var result = p!.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch { return null; }
    }

    private static string Home() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string Combine(params string[] parts) =>
        Path.GetFullPath(Path.Combine(parts));

    private static string StripAnsi(string text)
    {
        // Strip ANSI escape codes like ESC[...m and ESC[...
        return System.Text.RegularExpressions.Regex.Replace(text, @"\[[^@-~]*[@-~]|[^@-~]", "");
    }

    private void Log(string message)    => OnLogOutput?.Invoke(StripAnsi(message));
    private void Status(string message) => OnStatusChanged?.Invoke(message);

    private static string Quote(string s) =>
        s.Contains(' ') ? $"\"{s}\"" : s;
}
