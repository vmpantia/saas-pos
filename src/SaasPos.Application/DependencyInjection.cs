using Microsoft.Extensions.DependencyInjection;
using SaasPos.Application.Services;

namespace SaasPos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection s) => s
        .AddScoped<AuthService>()
        .AddScoped<CatalogService>()
        .AddScoped<InventoryService>()
        .AddScoped<SaleService>()
        .AddScoped<ReportService>();
}
