using Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Api.Middleware;

/// <summary>Traduz excecoes em ProblemDetails com o status HTTP adequado.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted) throw;
            var problem = Map(ex);
            if (problem.Status >= 500)
                logger.LogError("Erro HTTP 500 nao tratado. Tipo={FailureType} TraceId={TraceId} Method={Method} Path={Path}",
                    ex.GetType().Name, context.TraceIdentifier, context.Request.Method, context.Request.Path);
            problem.Instance = context.Request.Path;
            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;
            context.Response.ContentType = "application/problem+json";
            context.Response.Headers.CacheControl = "no-store";
            // Preserva Errors de ValidationProblemDetails na serializacao do tipo derivado.
            if (problem is ValidationProblemDetails validation)
                await context.Response.WriteAsJsonAsync(validation, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
            else
                await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
        }
    }

    private static ProblemDetails Map(Exception ex) => ex switch
    {
        ValidationException v => new ValidationProblemDetails(v.Errors.GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
        { Status = 400, Title = "Erro de validacao" },
        NotFoundException => Problem(404, "Recurso nao encontrado", ex.Message),
        ConflictException => Problem(409, "Conflito", ex.Message),
        BusinessRuleException => Problem(400, "Regra de negocio violada", ex.Message),
        ForbiddenException => Problem(403, "Acesso negado", ex.Message),
        RateLimitException => Problem(429, "Limite temporário", "Aguarde antes de solicitar novamente."),
        UnauthorizedException => Problem(401, "Nao autenticado", ex.Message),
        DbUpdateException => Problem(409, "Conflito de dados", "A operacao viola uma restricao do banco (duplicidade ou registros dependentes)."),
        _ => Problem(500, "Erro interno", "Ocorreu um erro inesperado.")
    };

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
