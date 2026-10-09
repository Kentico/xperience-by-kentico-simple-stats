using CMS;
using CMS.Activities.Internal;
using CMS.Core;
using CMS.DataEngine;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using Samples.DancingGoat;

[assembly: RegisterModule(typeof(UtmTrackingModule))]

namespace Samples.DancingGoat;

/// <summary>
/// Registers <see cref="UtmActivityModifier"/>.
/// </summary>
internal class UtmTrackingModule : Module
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UtmTrackingModule"/> class.
    /// </summary>
    public UtmTrackingModule() : base(nameof(UtmTrackingModule))
    {
    }


    /// <inheritdoc />
    protected override void OnInit(ModuleInitParameters parameters)
    {
        base.OnInit(parameters);

        ActivityEvaluationRegister.Instance.Modifiers.Register(() =>
        {
            var httpContextAccessor = parameters.Services.GetRequiredService<IHttpContextAccessor>();

            return new UtmActivityModifier(httpContextAccessor);
        });
    }
}
