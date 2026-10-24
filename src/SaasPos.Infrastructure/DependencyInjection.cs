using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SaasPos.Application.Abstractions;
using SaasPos.Domain.Entities;
using SaasPos.Infrastructure.Persistence;
using SaasPos.Infrastructure.Security;

namespace SaasPos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration config)
    {
        s.Configure<JwtOptions>(config.GetSection("Jwt"));
        s.AddDbContext<AppDbContext>(o => o.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=saaspos.db"));
        s.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        s.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        s.AddScoped<IProductRepository, ProductRepository>();
        s.AddScoped<ISaleRepository, SaleRepository>();
        s.AddScoped<ITenantRepository, TenantRepository>();
        s.AddScoped<IUserRepository, UserRepository>();
        s.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        s.AddSingleton<ITokenService, JwtTokenService>();
        return s;
    }

    /// <summary>Dev convenience: creates the schema and a demo tenant.</summary>
    public static async Task InitializeDevelopmentAsync(this IServiceProvider root)
    {
        using var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        if (await db.Tenants.AnyAsync()) return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var tenant = new Tenant("Demo Store", "demo");
        db.Tenants.Add(tenant);
        db.Users.Add(new User(tenant.Id, "admin", hasher.Hash("Admin123!"), "Demo Admin", SaasPos.Domain.Enums.UserRole.Admin));
        await db.SaveChangesAsync();
    }
}
