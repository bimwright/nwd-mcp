using Newtonsoft.Json.Linq;

namespace Bimwright.Nwd.Shared.Infrastructure;

/// <summary>
/// Host busy (family contract FC-BUSY-01..05; ipt-mcp HostBusyPolicy parity). When Navisworks cannot start a command
/// because a modal dialog is waiting for the user or its UI thread stayed busy until the start deadline, the call
/// returns <c>HOST_BUSY</c> and says the command was not started; that command never runs later. A command that
/// started and then ran past its timeout stays <c>TIMEOUT</c> with an unknown outcome. Host-free so it is unit-tested.
/// </summary>
public static class HostBusyPolicy
{
    public const string Code = "HOST_BUSY";
    public const string ModalDialog = "modal_dialog";
    public const string UiThreadBusy = "ui_thread_busy";

    /// <summary>How long health waits for the UI thread before it answers from the transport thread (FC-BUSY-05).</summary>
    public const int HealthUiWaitMs = 2000;

    private const string NotStarted = " The command was not started and will not run later; it is safe to send it again once Navisworks is free.";

    /// <summary>
    /// A modal dialog refuses every command that would run on the UI thread. Health answers from the transport thread
    /// instead, and report_task_result never touches Navisworks. Checked before queueing and again on the UI thread,
    /// because a dialog's message loop runs queued work.
    /// </summary>
    public static bool RefusesForModal(string? command, bool modalOpen)
        => modalOpen && command != "health_check" && command != "report_task_result";

    public static string ModalMessage(string? command, string? dialogTitle)
        => $"{command} refused: a modal dialog is open in Navisworks"
           + (string.IsNullOrEmpty(dialogTitle) ? "" : $" (\"{dialogTitle}\")")
           + " and is waiting for the user." + NotStarted
           + " Ask the user to answer or close the dialog, then check nwd_health_check (host_busy=false) before retrying.";

    public static string ExpiredMessage(string? command, int timeoutMs)
        => $"{command} refused: the Navisworks UI thread stayed busy with other work, so the command could not start within its {timeoutMs} ms timeout." + NotStarted
           + " Check nwd_health_check (host_busy) before retrying.";

    /// <summary>TIMEOUT for a command that started: its outcome is unknown (FC-BUSY-04).</summary>
    public static string StartedTimeoutMessage(string? command, int timeoutMs)
        => $"{command} started but did not finish within {timeoutMs} ms; it may still complete in Navisworks. "
           + "Its outcome is unknown: inspect the document before retrying a write.";

    /// <summary>Health fields (FC-BUSY-05): one flag and the reason.</summary>
    public static JObject WithHostBusy(this JObject data, bool modalOpen, bool uiThreadBusy)
    {
        data["host_busy"] = modalOpen || uiThreadBusy;
        data["busy_reason"] = modalOpen ? ModalDialog : uiThreadBusy ? UiThreadBusy : null;
        return data;
    }
}
