using System.Threading.Tasks;

using CMS.Websites;

using Kentico.OnlineMarketing.Web.Mvc;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace Samples.DancingGoat;

/// <summary>
/// Decorates <see cref="IWebPagesActivityLogger"/> to read UTM parameters from the URL of a logged landing page
/// into <see cref="UtmParameters"/>, where <see cref="UtmActivityModifier"/> picks them up.
/// </summary>
/// <remarks>
/// The landing page activity is logged by the activity tracking script in a separate request, so the UTM
/// parameters are only available in the page URL the script sends.
/// </remarks>
public class UtmWebPagesActivityLogger : IWebPagesActivityLogger
{
    private const string UTM_SOURCE_PARAMETER = "utm_source";
    private const string UTM_CONTENT_PARAMETER = "utm_content";

    private readonly IWebPagesActivityLogger webPagesActivityLogger;
    private readonly IHttpContextAccessor httpContextAccessor;


    /// <summary>
    /// Initializes a new instance of the <see cref="UtmWebPagesActivityLogger"/> class.
    /// </summary>
    public UtmWebPagesActivityLogger(IWebPagesActivityLogger webPagesActivityLogger, IHttpContextAccessor httpContextAccessor)
    {
        this.webPagesActivityLogger = webPagesActivityLogger;
        this.httpContextAccessor = httpContextAccessor;
    }


    /// <inheritdoc />
    public Task LogPageVisit(WebPageMetadata webPageMetadata, string alternativeWebPageName, string languageName, string activityValue = null, string activityUrl = null, string referrerUrl = null) =>
        webPagesActivityLogger.LogPageVisit(webPageMetadata, alternativeWebPageName, languageName, activityValue, activityUrl, referrerUrl);


    /// <inheritdoc />
    public Task LogLandingPage(WebPageMetadata webPageMetadata, string alternativeWebPageName, string languageName, string activityUrl = null, string referrerUrl = null)
    {
        StoreUtmParameters(activityUrl);

        return webPagesActivityLogger.LogLandingPage(webPageMetadata, alternativeWebPageName, languageName, activityUrl, referrerUrl);
    }


    private void StoreUtmParameters(string activityUrl)
    {
        var utmParameters = httpContextAccessor.HttpContext?.RequestServices.GetService<UtmParameters>();
        if (utmParameters is null || string.IsNullOrEmpty(activityUrl))
        {
            return;
        }

        var queryStart = activityUrl.IndexOf('?');
        if (queryStart < 0)
        {
            return;
        }

        var fragmentStart = activityUrl.IndexOf('#', queryStart);
        var query = fragmentStart < 0 ? activityUrl[queryStart..] : activityUrl[queryStart..fragmentStart];
        var parameters = QueryHelpers.ParseQuery(query);

        utmParameters.Source = parameters.TryGetValue(UTM_SOURCE_PARAMETER, out var source) ? source.ToString() : null;
        utmParameters.Content = parameters.TryGetValue(UTM_CONTENT_PARAMETER, out var content) ? content.ToString() : null;
    }
}
