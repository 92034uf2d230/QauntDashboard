using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuantDashboard.Models;

namespace QuantDashboard.Managers
{
    /// <summary>
    /// Singleton manager for application settings
    /// Handles loading, saving, and runtime modification of settings
    /// </summary>
    public sealed class SettingsManager
    {
        private static readonly Lazy<SettingsManager> _lazy =
            new Lazy<SettingsManager>(() => new SettingsManager());

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static SettingsManager Instance => _lazy.Value;

        private readonly string _settingsPath;
        private AppSettings _currentSettings;
        private FileSystemWatcher? _watcher;

        /// <summary>
        /// Current application settings
        /// </summary>
        public AppSettings CurrentSettings => _currentSettings;

        /// <summary>
        /// Event fired when settings are reloaded
        /// </summary>
        public event Action<AppSettings>? OnSettingsReloaded;

        private SettingsManager()
        {
            // Find settings file using multiple strategies
            _settingsPath = FindSettingsPath();
            _currentSettings = AppSettings.CreateDefault();
        }

        /// <summary>
        /// Find settings.json path using multiple strategies:
        /// 1. Environment variable QUANT_SETTINGS_PATH
        /// 2. Current working directory
        /// 3. Executable directory
        /// 4. Search parent directories from executable location
        /// </summary>
        private static string FindSettingsPath()
        {
            const string settingsFileName = "settings.json";

            // Strategy 1: Check environment variable
            var envPath = Environment.GetEnvironmentVariable("QUANT_SETTINGS_PATH");
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            {
                return envPath;
            }

            // Strategy 2: Check current working directory
            var cwdPath = Path.Combine(Directory.GetCurrentDirectory(), settingsFileName);
            if (File.Exists(cwdPath))
            {
                return cwdPath;
            }

            // Strategy 3: Check executable directory
            var exePath = Path.Combine(AppContext.BaseDirectory, settingsFileName);
            if (File.Exists(exePath))
            {
                return exePath;
            }

            // Strategy 4: Search parent directories (up to 5 levels) for project root
            var searchDir = AppContext.BaseDirectory;
            for (int i = 0; i < 5; i++)
            {
                var parent = Directory.GetParent(searchDir);
                if (parent == null) break;

                var candidatePath = Path.Combine(parent.FullName, settingsFileName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }

                // Also check if this looks like a project root (has .sln or .csproj)
                if (Directory.GetFiles(parent.FullName, "*.sln").Length > 0 ||
                    Directory.GetFiles(parent.FullName, "*.csproj").Length > 0)
                {
                    // Return this path even if settings.json doesn't exist yet
                    return candidatePath;
                }

                searchDir = parent.FullName;
            }

            // Default: create in project root relative to executable
            // This handles typical development layout: bin/Debug/net10.0/
            var defaultBaseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            return Path.Combine(defaultBaseDir, settingsFileName);
        }

        /// <summary>
        /// Load settings from file. Creates default settings file if not exists.
        /// Supports environment variable override via QUANT_MODE.
        /// </summary>
        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    Console.WriteLine($"[SettingsManager] Settings file not found at: {_settingsPath}");
                    Console.WriteLine("[SettingsManager] Creating default settings file...");
                    _currentSettings = AppSettings.CreateDefault();
                    Save(_currentSettings);
                }
                else
                {
                    var json = File.ReadAllText(_settingsPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    };

                    _currentSettings = JsonSerializer.Deserialize<AppSettings>(json, options)
                                       ?? AppSettings.CreateDefault();

                    // Validate settings
                    if (!_currentSettings.Validate(out var errorMessage))
                    {
                        Console.WriteLine($"[SettingsManager] Invalid settings: {errorMessage}");
                        Console.WriteLine("[SettingsManager] Using default values for invalid fields.");
                    }

                    Console.WriteLine($"[SettingsManager] Loaded settings from: {_settingsPath}");
                }

                // Apply environment variable override for Mode
                var envMode = Environment.GetEnvironmentVariable("QUANT_MODE");
                if (!string.IsNullOrEmpty(envMode))
                {
                    Console.WriteLine($"[SettingsManager] Environment override: QUANT_MODE={envMode}");
                    _currentSettings.Mode = envMode;
                }

                Console.WriteLine($"[SettingsManager] Current Mode: {_currentSettings.Mode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsManager] Error loading settings: {ex.Message}");
                Console.WriteLine("[SettingsManager] Using default settings.");
                _currentSettings = AppSettings.CreateDefault();
            }

            return _currentSettings;
        }

        /// <summary>
        /// Save settings to file
        /// </summary>
        public void Save(AppSettings settings)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(_settingsPath, json);
                _currentSettings = settings;
                Console.WriteLine($"[SettingsManager] Settings saved to: {_settingsPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsManager] Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Enable file watcher to detect runtime changes
        /// </summary>
        public void EnableFileWatcher()
        {
            try
            {
                var directory = Path.GetDirectoryName(_settingsPath);
                var fileName = Path.GetFileName(_settingsPath);

                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    Console.WriteLine("[SettingsManager] Cannot enable file watcher: directory does not exist.");
                    return;
                }

                _watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
                };

                _watcher.Changed += OnFileChanged;
                _watcher.EnableRaisingEvents = true;

                Console.WriteLine("[SettingsManager] File watcher enabled for settings.json");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsManager] Error enabling file watcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Disable file watcher
        /// </summary>
        public void DisableFileWatcher()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= OnFileChanged;
                _watcher.Dispose();
                _watcher = null;
                Console.WriteLine("[SettingsManager] File watcher disabled");
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // Add small delay to ensure file write is complete
                System.Threading.Thread.Sleep(100);

                Console.WriteLine("[SettingsManager] Settings file changed, reloading...");
                var previousMode = _currentSettings.Mode;
                Load();

                if (previousMode != _currentSettings.Mode)
                {
                    Console.WriteLine($"[SettingsManager] Mode changed from {previousMode} to {_currentSettings.Mode}");
                    Console.WriteLine("[SettingsManager] Note: Mode changes require application restart to take effect.");
                }

                OnSettingsReloaded?.Invoke(_currentSettings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SettingsManager] Error reloading settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Update settings at runtime
        /// </summary>
        public void UpdateSettings(Action<AppSettings> updateAction)
        {
            updateAction(_currentSettings);

            if (!_currentSettings.Validate(out var errorMessage))
            {
                Console.WriteLine($"[SettingsManager] Invalid settings update: {errorMessage}");
                return;
            }

            Save(_currentSettings);
            OnSettingsReloaded?.Invoke(_currentSettings);
        }

        /// <summary>
        /// Get the path to the settings file
        /// </summary>
        public string SettingsFilePath => _settingsPath;
    }
}
