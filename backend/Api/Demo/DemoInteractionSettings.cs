using Infrastructure.Demo;
using Api.Security;
namespace Api.Demo;

public class DemoInteractionSettings
{
    public bool InteractiveAcceptance { get; set; } = false;
    public static bool IsAcceptanceRequest(HttpContext context, bool enabled) => enabled && HttpMethods.IsPost(context.Request.Method) &&
        string.Equals(context.Request.Path.Value?.TrimEnd('/'), $"/api/recommendations/{DemoEvaluationPlan.PreventiveRecommendationId:D}/accept", StringComparison.OrdinalIgnoreCase);
    public static bool CanPersist(HttpContext? context, bool enabled) => context is not null && IsAcceptanceRequest(context, enabled) &&
        context.User.Identity?.IsAuthenticated == true && context.User.IsManager();
}
