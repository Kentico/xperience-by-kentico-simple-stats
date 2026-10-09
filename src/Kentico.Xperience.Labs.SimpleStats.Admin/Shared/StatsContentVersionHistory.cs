using System.Globalization;

using CMS.ContentEngine.Internal;
using CMS.Core;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Content version history settings (Settings → Content → Content versioning). Reports that read <c>CMS_ContentItemVersion</c>
/// (publishing activity, editor contributions) show its data only while history is enabled, and hint that it is trimmed.
/// </summary>
internal static class StatsContentVersionHistory
{
    /// <summary>
    /// Settings key of "Enable content version history". The product constant is internal.
    /// </summary>
    public const string EnabledSettingsKey = "CMSContentVersionHistoryEnable";

    /// <summary>
    /// Settings key of the number of versions kept per language variant.
    /// </summary>
    public const string LengthSettingsKey = ContentItemVersionSettingsConstants.CONTENT_VERSION_HISTORY_LENGTH_SETTING_KEY;

    /// <summary>
    /// <c>true</c> when the setting is "True" (any casing); <c>false</c> when it is not set or has another value.
    /// The settings service caches settings, so this can be read on every request.
    /// </summary>
    public static bool IsEnabled(ISettingsService settingsService) =>
        bool.TryParse(settingsService[EnabledSettingsKey], out bool enabled) && enabled;

    /// <summary>
    /// Versions kept per variant, 0 when not set or not a number.
    /// </summary>
    public static int GetLength(ISettingsService settingsService) =>
        int.TryParse(settingsService[LengthSettingsKey], NumberStyles.Integer, CultureInfo.InvariantCulture, out int length)
            ? Math.Max(length, 0)
            : 0;
}
