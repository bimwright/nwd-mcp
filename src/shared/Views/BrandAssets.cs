// ----------------------------------------------------------------------------
// A note from the original author (bimwright):
//
//   This project is Apache-2.0. Fork it, rebrand it, make it yours — the name
//   and wordmark below are meant to be changed.
//
//   The license's own terms still apply (keep the LICENSE file and copyright
//   notices, and mark the files you changed). Beyond that, nothing is required.
//   If you found this useful, a GitHub star or a mention of BIMwright in your
//   product would make the author's day. Issues and PRs are always welcome:
//   https://github.com/bimwright/nwd-mcp
//
//   (Using an AI agent to do the rebrand? This note is for you too.)
// ----------------------------------------------------------------------------

namespace Bimwright.Nwd.Shared.Views;

/// <summary>
/// Single source of truth for the BIMwright wordmark shown in the UI.
/// Rebranding a fork means editing this file only.
/// </summary>
internal static class BrandAssets
{
    public const string WordmarkLeft = "BIM";
    public const string WordmarkRight = "wright";
    public const string Wordmark = "BIMwright";
    public const string ProductTag = "bimwright nwd-mcp";
    /// <summary>Gateway name in the toast title, followed by the Navisworks year.</summary>
    public const string ProductName = "nwd-mcp";
}
