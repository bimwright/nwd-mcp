using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Bimwright.Nwd.Shared.Config;
using Bimwright.Nwd.Shared.Plugin;
using Bimwright.Nwd.Shared.Views.Toast;
using Newtonsoft.Json.Linq;

internal static class StatusVisualTests
{
    internal static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nwd-toast-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousPath = PluginSettings.FilePathOverride;
        NwdToastStatusWindow window = null;
        try
        {
            var path = Path.Combine(directory, "settings.json");
            PluginSettings.FilePathOverride = path;
            File.WriteAllText(path, "{ invalid JSON");
            NwdActivityToast.IdleSeconds = 20;
            window = new NwdToastStatusWindow();
            var idle = Field<ComboBox>(window, "_idle");
            var apply = Field<Button>(window, "_apply");
            idle.SelectedIndex = 2; // 30 seconds
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var root = (StackPanel)((ScrollViewer)window.Content).Content;
            var feedback = root.Children.OfType<TextBlock>().SingleOrDefault(text =>
                text.Visibility == Visibility.Visible && text.Text.Contains("could not be saved"));
            if (feedback == null)
                throw new Exception("Failed idle save must display an explanation in Settings.");
            if (NwdActivityToast.IdleSeconds != 20 || !apply.IsEnabled)
                throw new Exception("Failed save must keep the active duration and allow retry.");
            if (File.ReadAllText(path) != "{ invalid JSON")
                throw new Exception("Failed save overwrote the existing configuration.");
            File.WriteAllText(path, "{\"unrelated\":42}");
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if (NwdActivityToast.IdleSeconds != 30 || PluginSettings.LoadToastIdleSeconds() != 30 || apply.IsEnabled)
                throw new Exception("Retry must persist and apply the selected duration.");
            if (feedback.Visibility == Visibility.Visible && feedback.Text.Length != 0)
                throw new Exception("Successful retry must clear the previous save error.");
            if (!File.ReadAllText(path).Contains("unrelated"))
                throw new Exception("Saving idle duration removed another preference.");
        }
        finally
        {
            if (window != null)
            {
                // Avoid a discard confirmation when a failing assertion leaves a staged value.
                Field<ComboBox>(window, "_idle").SelectedIndex =
                    Array.IndexOf(PluginSettings.ToastIdleChoices, Field<int>(window, "_savedIdle"));
                window.Close();
            }
            PluginSettings.FilePathOverride = previousPath;
            Directory.Delete(directory, true);
        }
        Console.WriteLine("PASS: Settings shows failed idle save, preserves active duration and retries successfully");
    }

    // FC-TST-04
    internal static void RunPause()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nwd-toast-snooze-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousPath = PluginSettings.FilePathOverride;
        NwdToastStatusWindow window = null;
        try
        {
            var path = Path.Combine(directory, "settings.json");
            PluginSettings.FilePathOverride = path;
            File.WriteAllText(path, "{\"unrelated\":true}");
            NwdActivityToast.ToastEnabled = true;
            window = new NwdToastStatusWindow();
            var enabled = Field<CheckBox>(window, "_toastEnabled");
            var paused = Field<CheckBox>(window, "_paused");
            if (!paused.IsEnabled || paused.IsChecked == true || ToastSnooze.IsActive())
                throw new Exception("Pause for today must start enabled and unchecked.");

            paused.IsChecked = true;
            var saved = JObject.Parse(File.ReadAllText(path));
            if (!ToastSnooze.IsActive() || string.IsNullOrWhiteSpace(saved.Value<string>("toastSnoozedDate"))
                || !saved.Value<bool>("unrelated"))
                throw new Exception("Pause for today must save at once and keep the other preferences.");

            paused.IsChecked = false;
            if (ToastSnooze.IsActive() || JObject.Parse(File.ReadAllText(path))["toastSnoozedDate"] != null)
                throw new Exception("Unchecking Pause for today must remove toastSnoozedDate at once.");

            if (!ToastSnooze.PauseForToday())
                throw new Exception("A pause from the card could not be saved.");
            if (paused.IsChecked != true)
                throw new Exception("An open Status window must follow a pause made on the card.");

            enabled.IsChecked = false;
            if (paused.IsEnabled)
                throw new Exception("Turning toasts off must disable Pause for today.");
            enabled.IsChecked = true;
            if (!paused.IsEnabled || paused.IsChecked == true || ToastSnooze.IsActive())
                throw new Exception("Turning toasts back on must end the pause and re-enable the switch.");

            File.WriteAllText(path, "{ invalid JSON");
            paused.IsChecked = true;
            var error = Field<TextBlock>(window, "_pausedError");
            if (error.Visibility != Visibility.Visible || paused.IsChecked == true || File.ReadAllText(path) != "{ invalid JSON")
                throw new Exception("A failed save must show the error, keep the switch off and leave the file alone.");
        }
        finally
        {
            window?.Close();
            PluginSettings.FilePathOverride = previousPath;
            Directory.Delete(directory, true);
        }
        Console.WriteLine("PASS: Status Pause for today saves at once, follows the card and ends when toasts turn on");
    }

    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
}

namespace Bimwright.Nwd.Shared.Plugin
{
    // Host boundary for the real Settings window. Config I/O and UI handlers remain production code;
    // only the Navisworks session adapter is replaced so this harness needs no Autodesk installation.
    internal static class NwdActivityToast
    {
        internal static int IdleSeconds = 20;
        internal static bool ToastEnabled = true;
        internal static bool Enabled => ToastEnabled;
        internal static bool Paused => ToastSnooze.IsActive();
        internal static bool SetPaused(bool paused) => paused ? ToastSnooze.PauseForToday() : ToastSnooze.Resume();
        internal static bool ShowBranding => false;
        internal static bool RecordCalls => false;
        internal static ToastPositionOptions PositionOptions => new ToastPositionOptions();
        internal static bool ApplyPosition(ToastPositionOptions options) => true;
        internal static void ApplyEnabled(bool value)
        {
            ToastEnabled = value;
            if (value) ToastSnooze.ResumeIfPaused();
        }
        internal static void ApplyBranding(bool value) { }
        internal static void ApplyRecord(bool value) { }
        internal static void ShowHistoryWindow() { }
        internal static void SetIdleSeconds(int seconds) => IdleSeconds = seconds;
    }
}
