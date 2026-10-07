using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(StatsContentSection),
    parentType: typeof(StatsApplicationPage),
    slug: "content",
    name: "Content",
    templateName: TemplateNames.SECTION_LAYOUT,
    order: 200,
    Icon = Icons.TreeStructure)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Content reports: content inventory and publishing calendar.
/// </summary>
public sealed class StatsContentSection(
    IUIPermissionEvaluator permissionEvaluator,
    IPageLinkGenerator pageLinkGenerator) : StatsSectionPage(permissionEvaluator, pageLinkGenerator)
{
}
