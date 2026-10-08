using CMS.Activities;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Samples.DancingGoat;

/// <summary>
/// Sets UTM source and content of landing page activities from <see cref="UtmParameters"/>, filled by <see cref="UtmWebPagesActivityLogger"/>.
/// </summary>
/// <remarks>
/// The modifier is a singleton, so the request-scoped <see cref="UtmParameters"/> is resolved per activity from the current request services.
/// </remarks>
public class UtmActivityModifier : IActivityModifier
{
    private const int UTM_COLUMN_MAX_LENGTH = 200;

    private readonly IHttpContextAccessor httpContextAccessor;


    /// <summary>
    /// Initializes a new instance of the <see cref="UtmActivityModifier"/> class.
    /// </summary>
    public UtmActivityModifier(IHttpContextAccessor httpContextAccessor)
    {
        this.httpContextAccessor = httpContextAccessor;
    }


    /// <inheritdoc />
    public void Modify(IActivityInfo activity)
    {
        if (activity.ActivityType != PredefinedActivityType.LANDING_PAGE)
        {
            return;
        }

        var utmParameters = httpContextAccessor.HttpContext?.RequestServices.GetService<UtmParameters>();
        if (utmParameters is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(utmParameters.Source))
        {
            activity.ActivityUTMSource = Truncate(utmParameters.Source);
        }

        if (!string.IsNullOrEmpty(utmParameters.Content))
        {
            activity.ActivityUTMContent = Truncate(utmParameters.Content);
        }
    }


    private static string Truncate(string value) =>
        value.Length > UTM_COLUMN_MAX_LENGTH ? value[..UTM_COLUMN_MAX_LENGTH] : value;
}
