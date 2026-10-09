namespace Samples.DancingGoat;

/// <summary>
/// Request-scoped holder of the UTM parameters of the landing page being logged.
/// </summary>
public class UtmParameters
{
    /// <summary>
    /// Value of the <c>utm_source</c> parameter.
    /// </summary>
    public string Source { get; set; }


    /// <summary>
    /// Value of the <c>utm_content</c> parameter.
    /// </summary>
    public string Content { get; set; }
}
