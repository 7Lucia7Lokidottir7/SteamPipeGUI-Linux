using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Main UI controller. Connects UIToolkit with logic layers.
/// Requires UIDocument on the same GameObject.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainWindowController : MonoBehaviour
{
    // ─── Init ────────────────────────────────────────────────────────

    private UIDocument       _doc;
    private VisualElement    _root;
    private AppConfig        _config;
    private SteamCmdWrapper  _steam;

    // Core elements
    private Label     _statusLabel;
    private Label     _logOutput;
    private ScrollView _logScroll;
    private int       _logLineCount;

    // Navigation
    private VisualElement _activePanelEl;
    private Button        _activeNavBtn;

    // Panels
    private VisualElement _panelLogin;
    private VisualElement _panelBuild;
    private VisualElement _panelSettings;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void OnEnable()
    {
        _doc    = GetComponent<UIDocument>();
        _root   = _doc.rootVisualElement;
        _config = AppConfig.Load();
        _steam  = new SteamCmdWrapper();

        // Apply SDK folder or direct path from config
        if (!string.IsNullOrEmpty(_config.SdkFolder))
            _steam.TrySetSdkFolder(_config.SdkFolder);
        else if (!string.IsNullOrEmpty(_config.SteamCmdPath))
            _steam.SetSteamCmdPath(_config.SteamCmdPath);

        _steam.OnLogOutput     += AppendLog;
        _steam.OnStatusChanged += UpdateStatus;

        BindElements();
        RestoreFormValues();
        ShowPanel(_panelLogin, _root.Q<Button>("btn-login"));

        AppendLog("SteamPipeGUI started. Please log in to get started.");
    }

    private void OnDisable()
    {
        _steam.OnLogOutput     -= AppendLog;
        _steam.OnStatusChanged -= UpdateStatus;
        SaveFormValues();
        _config.Save();
    }

    // ─── Bind elements ───────────────────────────────────────────────────

    private void BindElements()
    {
        // Header
        _statusLabel = _root.Q<Label>("status-label");
        _logOutput   = _root.Q<Label>("log-output");
        _logScroll   = _root.Q<ScrollView>("log-scroll");

        // Panels
        _panelLogin    = _root.Q<VisualElement>("panel-login");
        _panelBuild    = _root.Q<VisualElement>("panel-build");
        _panelSettings = _root.Q<VisualElement>("panel-settings");

        // Navigation
        _root.Q<Button>("btn-login").clicked    += () => ShowPanel(_panelLogin,    _root.Q<Button>("btn-login"));
        _root.Q<Button>("btn-build").clicked    += () => ShowPanel(_panelBuild,    _root.Q<Button>("btn-build"));
        _root.Q<Button>("btn-settings").clicked += () => ShowPanel(_panelSettings, _root.Q<Button>("btn-settings"));

        // Banner "steamcmd not found"
        RefreshSteamCmdBanner();
        _root.Q<Button>("btn-banner-go-settings").clicked += () => ShowPanel(_panelSettings, _root.Q<Button>("btn-settings"));

        // Login panel buttons
        _root.Q<Button>("btn-do-login").clicked  += OnLoginClicked;
        _root.Q<Button>("btn-do-logout").clicked += OnLogoutClicked;

        // Build panel buttons
        _root.Q<Button>("btn-browse-path").clicked += OnBrowsePathClicked;
        _root.Q<Button>("btn-start-build").clicked += OnStartBuildClicked;

        // Settings panel buttons
        _root.Q<Button>("btn-browse-sdk").clicked      += OnBrowseSdkFolderClicked;
        _root.Q<Button>("btn-browse-steamcmd").clicked += OnBrowseSteamCmdClicked;
        _root.Q<Button>("btn-save-settings").clicked   += OnSaveSettingsClicked;
        _root.Q<Button>("btn-clear-log").clicked       += () => { _logOutput.text = ""; _logLineCount = 0; };

        // Branch dropdown
        var branch = _root.Q<DropdownField>("dropdown-branch");
        branch.choices = new List<string> { "default", "beta", "staging", "preview", "public" };
    }

    // ─── Navigation ────────────────────────────────────────────────────────────

    private void ShowPanel(VisualElement panel, Button navBtn)
    {
        foreach (var p in new[] { _panelLogin, _panelBuild, _panelSettings })
            if (p != null) p.style.display = DisplayStyle.None;

        panel.style.display = DisplayStyle.Flex;
        _activePanelEl = panel;

        if (_activeNavBtn != null)
            _activeNavBtn.RemoveFromClassList("nav-button--active");
        navBtn?.AddToClassList("nav-button--active");
        _activeNavBtn = navBtn;
    }

    // ─── steamcmd banner ──────────────────────────────────────────────────────

    private void RefreshSteamCmdBanner()
    {
        var banner = _root.Q<VisualElement>("banner-no-steamcmd");
        if (banner == null) return;
        banner.style.display = _steam.IsSteamCmdFound ? DisplayStyle.None : DisplayStyle.Flex;
    }



    // ─── Login ────────────────────────────────────────────────────────────────

    private async void OnLoginClicked()
    {
        var username  = _root.Q<TextField>("field-username").value.Trim();
        var password  = _root.Q<TextField>("field-password").value;
        var guardCode = _root.Q<TextField>("field-steamguard").value.Trim();

        if (string.IsNullOrEmpty(username))
        {
            AppendLog("[ERROR] Enter a username.");
            return;
        }

        _root.Q<Button>("btn-do-login").SetEnabled(false);
        await _steam.LoginAsync(username, password, guardCode);
        _root.Q<Button>("btn-do-login").SetEnabled(true);

        // Show/hide logout button
        _root.Q<Button>("btn-do-logout").style.display =
            _steam.IsLoggedIn ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnLogoutClicked()
    {
        _steam.Logout();
        _root.Q<Button>("btn-do-logout").style.display = DisplayStyle.None;
    }

    // ─── Build ────────────────────────────────────────────────────────────────

    private void OnBrowsePathClicked()
    {
        NativeFilePicker.OpenFolderAsync(path =>
        {
            if (!string.IsNullOrEmpty(path))
            {
                _root.Q<TextField>("field-content-path").value = path;
                _config.DefaultContentPath = path;
            }
        }, "Select content folder");
    }

    private async void OnStartBuildClicked()
    {
        var appId       = _root.Q<TextField>("field-appid").value.Trim();
        var description = _root.Q<TextField>("field-build-desc").value.Trim();
        var contentPath = _root.Q<TextField>("field-content-path").value.Trim();
        var branch      = _root.Q<DropdownField>("dropdown-branch").value;
        var setLive     = _root.Q<Toggle>("toggle-live").value;

        if (string.IsNullOrEmpty(appId))
        {
            AppendLog("[ERROR] Enter App ID."); return;
        }
        if (string.IsNullOrEmpty(contentPath))
        {
            AppendLog("[ERROR] Enter content folder path."); return;
        }
        if (!_steam.IsLoggedIn)
        {
            AppendLog("[ERROR] Please log in to Steam first."); return;
        }

        _root.Q<Button>("btn-start-build").SetEnabled(false);
        AppendLog($"[INFO] Starting build for App {appId}...");

        await _steam.BuildAndUploadAsync(appId, description, contentPath, branch, setLive);

        _root.Q<Button>("btn-start-build").SetEnabled(true);
    }

    // ─── Settings ─────────────────────────────────────────────────────────────

    private void OnBrowseSteamCmdClicked()
    {
        NativeFilePicker.OpenFileAsync(path =>
        {
            if (!string.IsNullOrEmpty(path))
                _root.Q<TextField>("field-steamcmd-path").value = path;
        }, "Path to steamcmd");
    }

    private void OnBrowseSdkFolderClicked()
    {
        NativeFilePicker.OpenFolderAsync(path =>
        {
            if (string.IsNullOrEmpty(path)) return;
            _root.Q<TextField>("field-sdk-folder").value = path;
            var found = _steam.TrySetSdkFolder(path);
            _root.Q<Label>("label-steamcmd-resolved").text = found
                ? $"✓ {System.IO.Path.Combine(path, "tools/ContentBuilder/builder_linux/steamcmd.sh")}"
                : "✗ steamcmd.sh not found in this folder";
            RefreshSteamCmdBanner();
        }, "Select Steamworks SDK folder");
    }

    private void OnSaveSettingsClicked()
    {
        var sdkFolder     = _root.Q<TextField>("field-sdk-folder").value.Trim();
        var directPath    = _root.Q<TextField>("field-steamcmd-path").value.Trim();

        _config.SdkFolder    = sdkFolder;
        _config.SteamCmdPath = directPath;
        _config.LogMaxLines  = int.TryParse(_root.Q<TextField>("field-log-lines").value, out var lines)
            ? lines : 500;

        // SDK folder takes priority over direct path
        if (!string.IsNullOrEmpty(sdkFolder))
            _steam.TrySetSdkFolder(sdkFolder);
        else if (!string.IsNullOrEmpty(directPath))
            _steam.SetSteamCmdPath(directPath);

        RefreshSteamCmdBanner();
        _config.Save();
        AppendLog("[OK] Settings saved.");
    }

    // ─── Log ─────────────────────────────────────────────────────────────────

    private void AppendLog(string message)
    {
        UnityMainThreadDispatcher.Enqueue(() =>
        {
            _logLineCount++;

            // Trim old lines if limit exceeded
            if (_logLineCount > _config.LogMaxLines)
            {
                var lines = _logOutput.text.Split('\n');
                _logOutput.text = string.Join("\n", lines, lines.Length / 2, lines.Length - lines.Length / 2);
                _logLineCount   = _config.LogMaxLines / 2;
            }

            _logOutput.text += $"\n[{System.DateTime.Now:HH:mm:ss}] {message}";

            // Scroll to bottom after one frame
            _logScroll.schedule.Execute(() =>
                _logScroll.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue)
            ).StartingIn(50);
        });
    }

    private void UpdateStatus(string status)
    {
        UnityMainThreadDispatcher.Enqueue(() =>
            _statusLabel.text = status
        );
    }

    // ─── Save / restore fields ───────────────────────────────────

    private void RestoreFormValues()
    {
        var usernameField = _root.Q<TextField>("field-username");
        if (usernameField != null)
            usernameField.value = _config.LastUsername;

        var appIdField = _root.Q<TextField>("field-appid");
        if (appIdField != null)
            appIdField.value = _config.LastAppId;

        var contentPathField = _root.Q<TextField>("field-content-path");
        if (contentPathField != null)
            contentPathField.value = _config.DefaultContentPath;

        var branchDropdown = _root.Q<DropdownField>("dropdown-branch");
        if (branchDropdown != null)
            branchDropdown.value = _config.LastBranch;

        var toggleLive = _root.Q<Toggle>("toggle-live");
        if (toggleLive != null)
            toggleLive.value = _config.SetLiveAfterUpload;

        var sdkFolderField = _root.Q<TextField>("field-sdk-folder");
        if (sdkFolderField != null)
            sdkFolderField.value = _config.SdkFolder;

        var steamCmdField = _root.Q<TextField>("field-steamcmd-path");
        if (steamCmdField != null)
            steamCmdField.value = _config.SteamCmdPath;
    }

    private void SaveFormValues()
    {
        _config.LastUsername       = _root.Q<TextField>("field-username")?.value ?? "";
        _config.LastAppId          = _root.Q<TextField>("field-appid")?.value ?? "";
        _config.DefaultContentPath = _root.Q<TextField>("field-content-path")?.value ?? "";
        _config.LastBranch         = _root.Q<DropdownField>("dropdown-branch")?.value ?? "default";
        _config.SetLiveAfterUpload = _root.Q<Toggle>("toggle-live")?.value ?? false;
    }
}
