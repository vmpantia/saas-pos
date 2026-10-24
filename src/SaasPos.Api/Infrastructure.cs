using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SaasPos.Application.Abstractions;
using SaasPos.Application.Dtos;
using SaasPos.Application.Services;
using SaasPos.Domain.Common;
using SaasPos.Infrastructure.Security;

namespace SaasPos.Api;

public class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid TenantId => Guid.TryParse(User?.FindFirstValue(JwtTokenService.TenantClaim), out var id) ? id : Guid.Empty;
    public Guid UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub"), out var id) ? id : Guid.Empty;
}

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                NotFoundException => (404, ex.Message),
                UnauthorizedAppException => (401, ex.Message),
                DomainException => (400, ex.Message),
                _ => (500, "An unexpected error occurred.")
            };
            if (status == 500) log.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title });
        }
    }
}
