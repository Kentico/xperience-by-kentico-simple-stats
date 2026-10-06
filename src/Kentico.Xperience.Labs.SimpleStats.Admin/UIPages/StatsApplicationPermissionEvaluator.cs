using CMS.Membership.Internal;

using Kentico.Xperience.Admin.Base.Authentication;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Evaluates permissions of the "Simple Stats (Labs)" application for pages outside of it (for example the web page "Stats" tab).
/// <see cref="Xperience.Admin.Base.IUIPermissionEvaluator"/> evaluates against the application of the current page, so it cannot be used there.
/// </summary>
internal interface IStatsApplicationPermissionEvaluator
{
    /// <summary>
    /// Whether the current user has <paramref name="permission"/> in the "Simple Stats (Labs)" application. Administrators always do.
    /// </summary>
    public Task<bool> IsGranted(string permission);
}

/// <remarks>
/// Uses <c>CMS.Membership.Internal</c> deliberately: there is no public API that evaluates a permission of another application.
/// </remarks>
internal sealed class StatsApplicationPermissionEvaluator(
    IAuthenticatedUserAccessor authenticatedUserAccessor,
    IApplicationPermissionEvaluator applicationPermissionEvaluator) : IStatsApplicationPermissionEvaluator
{
    private readonly IAuthenticatedUserAccessor authenticatedUserAccessor = authenticatedUserAccessor;
    private readonly IApplicationPermissionEvaluator applicationPermissionEvaluator = applicationPermissionEvaluator;

    public async Task<bool> IsGranted(string permission)
    {
        var user = await authenticatedUserAccessor.Get();
        if (user is null)
        {
            return false;
        }

        return user.IsAdministrator()
            || applicationPermissionEvaluator.Evaluate(new ApplicationPermissionEvaluationContext
            {
                User = user,
                ApplicationName = StatsApplicationPage.IDENTIFIER,
                PermissionName = permission,
            });
    }
}
