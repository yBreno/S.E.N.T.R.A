namespace Api.Demo;

/// <summary>Only the isolated Demo host uses this restriction; controllers and authorization stay unchanged.</summary>
public sealed class DemoReadOnlyMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var login = HttpMethods.IsPost(method) && string.Equals(context.Request.Path.Value?.TrimEnd('/'),
            "/api/auth/login", StringComparison.OrdinalIgnoreCase);
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || login)
            return next(context);
        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
        context.Response.Headers.Allow = "GET, HEAD, OPTIONS";
        return context.Response.WriteAsJsonAsync(new { message = "A API DEMO permite somente consultas e login." });
    }
}
