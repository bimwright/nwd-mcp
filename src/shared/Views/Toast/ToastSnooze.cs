#nullable disable
using System;
using System.Globalization;
using Bimwright.Nwd.Shared.Config;

namespace Bimwright.Nwd.Shared.Views.Toast
{
    /// <summary>
    /// "Pause for today": activity toasts stay silent in every Navisworks on this computer until the next local
    /// midnight. The pause is the local date saved in nwdmcp.config.json (toastSnoozedDate), read on each check, so a
    /// pause set in one Navisworks applies to the others without a timer (FC-TST-04).
    /// </summary>
    public static class ToastSnooze
    {
        private const string DateFormat = "yyyy-MM-dd";

        /// <summary>Raised after this process paused or resumed toasts (the Status window refreshes its switch).</summary>
        public static event Action Changed;

        /// <summary>Test hook for the local clock.</summary>
        internal static Func<DateTime> Now { get; set; } = () => DateTime.Now;

        public static bool IsActive() => IsActive(PluginSettings.LoadToastSnoozedDate(), Now());

        public static bool IsActive(string snoozedDate, DateTime now) =>
            DateTime.TryParseExact(snoozedDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && date.Date == now.Date;

        /// <summary>Pauses toasts for the rest of today. Returns whether the choice was saved.</summary>
        public static bool PauseForToday() => Save(Now().ToString(DateFormat, CultureInfo.InvariantCulture));

        /// <summary>Ends a pause early. Returns whether the choice was saved.</summary>
        public static bool Resume() => Save(null);

        /// <summary>
        /// Turning toasts on means showing them now, so an active pause ends too. Returns false only when a pause was
        /// active and its removal could not be saved.
        /// </summary>
        public static bool ResumeIfPaused() => !IsActive() || Resume();

        private static bool Save(string date)
        {
            var saved = PluginSettings.SaveToastSnoozedDate(date);
            try { Changed?.Invoke(); } catch { }
            return saved;
        }
    }
}
