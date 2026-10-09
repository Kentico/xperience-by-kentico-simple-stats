using System.Text.Json.Serialization;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// Meaning of a ranked item for chart colors (see <see cref="StatsRankedItem.Tone"/>). The admin client maps it to
/// traffic-light colors: <see cref="Problem"/> red, <see cref="Caution"/> yellow, <see cref="Done"/> green,
/// <see cref="Neutral"/> a palette color. Public because it is part of the public <see cref="StatsRankedItem"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<StatsTone>))]
public enum StatsTone
{
    /// <summary>No known meaning.</summary>
    Neutral,

    /// <summary>Something went wrong (for example a failed payment).</summary>
    Problem,

    /// <summary>Waiting or needs attention (for example pending).</summary>
    Caution,

    /// <summary>Done or good (for example fulfilled).</summary>
    Done,
}
