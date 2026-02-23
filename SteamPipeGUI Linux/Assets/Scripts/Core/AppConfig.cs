using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// App configuration. Stored in ~/.config/SteamPipeGUI/config.json
/// </summary>
[Serializable]
public class AppConfig
{
    // ─── Steam ────────────────────────────────────────────────────────────────
    public string LastUsername     { get; set; } = "";
    public string SdkFolder        { get; set; } = "";   // Steamworks SDK folder
    public string SteamCmdPath     { get; set; } = "";   // direct path to steamcmd.sh
    public string DefaultContentPath { get; set; } = ""; // last opened path

    // ─── Build ────────────────────────────────────────────────────────────────
    public string LastAppId        { get; set; } = "";
    public string LastBranch       { get; set; } = "default";
    public bool   SetLiveAfterUpload { get; set; } = false;

    // ─── UI ──────────────────────────────────────────────────────────────────
    public int    WindowWidth      { get; set; } = 1200;
    public int    WindowHeight     { get; set; } = 700;
    public bool   ShowLogPanel     { get; set; } = true;
    public int    LogMaxLines      { get; set; } = 500;

    // ─── Paths ────────────────────────────────────────────────────────────────
    private static string ConfigDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "SteamPipeGUI"
        );

    private static string ConfigFile => Path.Combine(ConfigDir, "config.json");

    // ─── Save / Load ──────────────────────────────────────────────────────────

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(ConfigFile, json);
            Debug.Log($"[AppConfig] Saved to {ConfigFile}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AppConfig] Failed to save: {ex.Message}");
        }
    }

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigFile))
            {
                var json = File.ReadAllText(ConfigFile);
                var config = JsonConvert.DeserializeObject<AppConfig>(json);
                Debug.Log("[AppConfig] Loaded from disk.");
                return config ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AppConfig] Failed to load: {ex.Message}");
        }

        Debug.Log("[AppConfig] No config found, using defaults.");
        return new AppConfig();
    }

    public void Reset()
    {
        try
        {
            if (File.Exists(ConfigFile))
                File.Delete(ConfigFile);
            Debug.Log("[AppConfig] Reset to defaults.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AppConfig] Failed to reset: {ex.Message}");
        }
    }
}
