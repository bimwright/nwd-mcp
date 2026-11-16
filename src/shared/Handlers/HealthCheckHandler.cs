#if NAVIS2022 || NAVIS2023 || NAVIS2024 || NAVIS2025 || NAVIS2026 || NAVIS2027
using Bimwright.Nwd.Shared.Infrastructure;
using Newtonsoft.Json.Linq;
using NW = Autodesk.Navisworks.Api;

namespace Bimwright.Nwd.Shared.Handlers;

public sealed class HealthCheckHandler : INwdCommand
{
    public string Name => "health_check";
    public bool IsReadOnly => true;
    private readonly JObject _identity;

    public HealthCheckHandler(int navisworksYear)
        => _identity = RuntimeIdentity.Capture(typeof(HealthCheckHandler).Assembly, "plugin", navisworksYear);

    public NwdCommandResult Execute(NwdCommandContext ctx, JObject p)
    {
        var meta = new NwdResponseMeta { TargetId = ctx.TargetId, NavisworksYear = ctx.NavisworksYear };
        var doc = NW.Application.ActiveDocument;
        var hasDoc = doc != null;

        var data = new JObject
        {
            ["healthy"] = true,
            ["navisworks_year"] = ctx.NavisworksYear,
            ["process_id"] = _identity["process_id"]!.DeepClone(),
            ["has_document"] = hasDoc,
            ["document_title"] = doc?.Title,
            ["setup_plugin"] = _identity.DeepClone(),
            // On the UI thread: busy only if a modal dialog's message loop ran this health check (FC-BUSY-05).
            ["modal_dialog"] = Plugin.ModalDialogProbe.Probe(),
        };
        data.WithHostBusy(modalOpen: data["modal_dialog"]?["open"]?.Value<bool>() == true, uiThreadBusy: false);
        return NwdCommandResult.Success(System.Guid.Empty, data, meta);
    }
}
#endif
