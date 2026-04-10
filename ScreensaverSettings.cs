using System;
using System.IO;
using System.Text.Json;

namespace HtmlScreensaver
{
    public enum ExitButtonMode
    {
        Hidden = 0,       // No button — Esc / mouse move / keypress exits
        AlwaysVisible = 1, // Button always shown in corner
        HoverVisible = 2   // Button fades in only when mouse moves
    }

    public class ScreensaverSettings
    {
        // ── Content ──────────────────────────────────────────────
        public string HtmlPath { get; set; } = "";
        public bool UseLocalhost { get; set; } = false;
        public int LocalhostPort { get; set; } = 8765;

        // ── Exit behaviour ────────────────────────────────────────
        public ExitButtonMode ExitMode { get; set; } = ExitButtonMode.HoverVisible;

        // Seconds of mouse inactivity before button fades out again (HoverVisible only)
        public int HoverFadeDelaySecs { get; set; } = 3;

        // How many pixels the mouse must move before triggering exit (without button)
        public int MouseMoveThresholdPx { get; set; } = 20;

        // ── Appearance ────────────────────────────────────────────
        // Corner for the exit button: 0=TopRight, 1=TopLeft, 2=BottomRight, 3=BottomLeft
        public int ButtonCorner { get; set; } = 0;

        // ── Persistence ──────────────────────────────────────────
        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HtmlScreensaver",
                "settings.json");

        public static ScreensaverSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<ScreensaverSettings>(json)
                           ?? new ScreensaverSettings();
                }
            }
            catch { /* fall through to defaults */ }
            return new ScreensaverSettings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch { /* ignore write errors silently */ }
        }
    }
}
