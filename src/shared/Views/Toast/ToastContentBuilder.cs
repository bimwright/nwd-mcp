#nullable disable
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Bimwright.Nwd.Shared.Localization;
using Bimwright.Nwd.Shared.Security;

namespace Bimwright.Nwd.Shared.Views.Toast
{
    /// <summary>
    /// Builds toast copy from an nwd command result. The wire payload is the
    /// NwdCommandResult envelope ({ ok, data, error }), not the bare data object.
    /// </summary>
    public static class ToastContentBuilder
    {
        public static McpToastViewModel BuildCompleted(
            string toolName,
            string resultJson,
            bool success,
            string errorMessage,
            string toolDescription)
        {
            if (string.IsNullOrEmpty(toolName))
                toolName = "unknown";

            var payload = UnwrapEnvelope(resultJson, ref success, ref errorMessage, out var durationMs);
            // Script/macro failures may live inside an outer successful transport result.
            var data = payload as JObject;
            if (success && data?["ok"] is JValue innerOk
                && innerOk.Type == Newtonsoft.Json.Linq.JTokenType.Boolean && !(bool)innerOk)
            {
                success = false;
                errorMessage = Preview(data["error"]) ?? L.T("toast.failed.default");
            }
            var content = success
                ? BuildSuccessContent(toolName, payload)
                : BuildFailureContent(toolName, errorMessage, toolDescription);

            return new McpToastViewModel
            {
                Title = ToastActivityEntry.Compact(ToolNameFormatter.Format(toolName), ToastActivityEntry.TitleLimit),
                Summary = ToastActivityEntry.Compact(content.Summary, ToastActivityEntry.BodyLimit),
                Detail = ToastActivityEntry.Compact(content.Detail, 100),
                ThumbnailPath = content.ThumbnailPath,
                Success = success,
                DurationMs = durationMs
            };
        }

        private static JToken UnwrapEnvelope(string resultJson, ref bool success, ref string errorMessage,
            out long? durationMs)
        {
            durationMs = null;
            JToken result = null;
            try { if (!string.IsNullOrEmpty(resultJson)) result = JToken.Parse(resultJson); } catch { }

            var obj = result as JObject;
            if (obj == null || obj["ok"] == null)
                return result;

            if (obj["ok"] is JValue ok && ok.Type == JTokenType.Boolean && !(bool)ok)
                success = false;
            if (obj["meta"]?["duration_ms"] is JValue elapsed && elapsed.Type == JTokenType.Integer)
                durationMs = Math.Max(0, elapsed.Value<long>());

            var err = obj["error"] as JObject;
            if (err != null && string.IsNullOrWhiteSpace(errorMessage))
                errorMessage = err.Value<string>("message");

            if (obj.Property("data") != null)
                return obj["data"];
            return result;
        }

        private static ToastContent BuildSuccessContent(string toolName, JToken result)
        {
            var resultObj = result as JObject;
            switch (toolName)
            {
                case "get_document_info":
                    return BuildDocumentInfo(resultObj);
                case "send_code":
                    return BuildSendCodeSuccess(resultObj);
                case "report_task_result":
                    // Agent-reported outcome (FC-RPT-01): the summary, with the outcome as detail.
                    return new ToastContent(Truncate(resultObj?.Value<string>("summary") ?? "", 120), resultObj?.Value<string>("outcome"));
                case "hide_items":
                    var hidden = resultObj?.Value<int?>("hidden_count");
                    if (hidden.HasValue)
                        return new ToastContent(L.T(resultObj.Value<bool?>("hide") == false
                            ? "toast.items.shown" : "toast.items.hidden", ("count", hidden.Value)), null);
                    return BuildGenericSuccess(toolName, result);
                case "select_items_by_search":
                    var selected = resultObj?.Value<int?>("selected_count");
                    return selected.HasValue
                        ? new ToastContent(L.T("toast.items.selected", ("count", selected.Value)), null)
                        : BuildGenericSuccess(toolName, result);
                case "execute_search_set":
                    var matched = resultObj?.Value<int?>("matched_count");
                    return matched.HasValue
                        ? new ToastContent(L.T(resultObj.Value<bool?>("selected") == true
                            ? "toast.items.selected" : "toast.items.matched", ("count", matched.Value)), null)
                        : BuildGenericSuccess(toolName, result);
                default:
                    return BuildGenericSuccess(toolName, result);
            }
        }

        private static ToastContent BuildFailureContent(string toolName, string errorMessage, string toolDescription)
        {
            var summary = Truncate(errorMessage ?? L.T("toast.failed.default"), 120);
            var detail = !string.IsNullOrWhiteSpace(toolDescription)
                ? Truncate(toolDescription, 100)
                : ToolNameFormatter.Format(toolName);
            return new ToastContent(summary, detail);
        }

        private static ToastContent BuildDocumentInfo(JObject data)
        {
            var title = data?.Value<string>("title");
            var file = data?.Value<string>("file_name");
            var name = !string.IsNullOrWhiteSpace(title)
                ? title
                : GetFileNameSafe(file, L.T("toast.document.untitled"));
            var models = data?.Value<int?>("model_count");
            var detail = models.HasValue
                ? L.T("toast.document.models", ("count", models.Value))
                : string.Empty;
            return new ToastContent(name, detail);
        }

        private static ToastContent BuildSendCodeSuccess(JObject result)
        {
            // Never stringify arbitrary script objects or retain the source request.
            var text = Preview(result?["stdout"]);
            if (string.IsNullOrWhiteSpace(text))
                text = Preview(result?["result"]);
            var summary = string.IsNullOrWhiteSpace(text)
                ? L.T("toast.sendCode.finished")
                : Truncate(FirstLine(text), 100);
            return new ToastContent(summary, L.T("toast.sendCode.detail"));
        }

        private static ToastContent BuildGenericSuccess(string toolName, JToken result)
        {
            if (result == null)
                return new ToastContent(L.T("toast.generic.completed"), ToolNameFormatter.Format(toolName));

            var array = result as JArray;
            if (array != null)
                return new ToastContent(L.T("toast.generic.items", ("count", array.Count)), ToolNameFormatter.Format(toolName));

            var obj = result as JObject;
            if (obj == null)
                return new ToastContent(L.T("toast.generic.completed"), ToolNameFormatter.Format(toolName));

            var total = obj.Value<int?>("total");
            var returned = obj.Value<int?>("returned");
            var count = obj.Value<int?>("count");
            var rowCount = obj.Value<int?>("rowCount");
            var savedPath = obj.Value<string>("saved_path") ?? obj.Value<string>("output_path") ?? obj.Value<string>("path");

            if (total.HasValue || returned.HasValue)
            {
                var n = total ?? returned;
                return new ToastContent(L.T("toast.generic.results", ("count", n.Value)), ToolNameFormatter.Format(toolName));
            }
            if (count.HasValue)
                return new ToastContent(L.T("toast.generic.items", ("count", count.Value)), ToolNameFormatter.Format(toolName));
            if (rowCount.HasValue)
                return new ToastContent(L.T("toast.generic.rows", ("count", rowCount.Value)), ToolNameFormatter.Format(toolName));
            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                var thumb = IsSafeImagePath(savedPath) ? savedPath : null;
                return new ToastContent(GetFileNameSafe(savedPath, L.T("toast.generic.fileFallback")), ToolNameFormatter.Format(toolName), thumb);
            }

            foreach (var prop in obj.Properties())
            {
                if (prop.Value is JArray propArray)
                    return new ToastContent(L.T("toast.generic.items", ("count", propArray.Count)), ToolNameFormatter.Format(toolName));
            }

            var message = obj.Value<string>("message") ?? obj.Value<string>("summary");
            if (!string.IsNullOrWhiteSpace(message))
                return new ToastContent(Truncate(message, 100), ToolNameFormatter.Format(toolName));

            return new ToastContent(L.T("toast.generic.completed"), ToolNameFormatter.Format(toolName));
        }

        internal static bool IsSafeImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                if (!File.Exists(path))
                    return false;

                var full = Path.GetFullPath(path);
                var ext = Path.GetExtension(full);
                if (ext == null)
                    return false;
                ext = ext.ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".bmp")
                    return false;

                return PathAllowlist.IsUnderTempOrCaptures(full);
            }
            catch
            {
                return false;
            }
        }

        private static string Preview(JToken token)
        {
            if (token is JArray array)
                return L.T("toast.generic.items", ("count", array.Count));
            if (token is JObject obj)
                return L.T("toast.generic.fields", ("count", obj.Count));
            if (token is JValue value && value.Type != JTokenType.Null)
                return Truncate(FirstLine(Convert.ToString(value.Value, System.Globalization.CultureInfo.InvariantCulture)), 120);
            return null;
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            var idx = text.IndexOf('\n');
            return idx < 0 ? text : text.Substring(0, idx);
        }

        private static string Truncate(string text, int max) => ToastActivityEntry.Compact(text, max);

        private readonly struct ToastContent
        {
            public string Summary { get; }
            public string Detail { get; }
            public string ThumbnailPath { get; }

            public ToastContent(string summary, string detail, string thumbnailPath = null)
            {
                Summary = summary ?? string.Empty;
                Detail = detail ?? string.Empty;
                ThumbnailPath = thumbnailPath;
            }
        }

        private static string GetFileNameSafe(string path, string fallback)
        {
            if (string.IsNullOrEmpty(path))
                return fallback;
            if (path.Contains("<") || path.Contains(">") || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                return fallback;
            try
            {
                return Path.GetFileName(path);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
