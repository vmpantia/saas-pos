# SaaS POS

Multi-tenant POS + inventory. Clean Architecture, .NET 10, Blazor Server frontend using MudBlazor.

```
src/SaasPos.Domain          Entities & business rules (Product stock, Sale totals/void). No dependencies.
src/SaasPos.Application     Use-case services, DTOs, abstractions (repositories, tokens, hashing).
src/SaasPos.Infrastructure  EF Core (SQLite), repositories, JWT, PBKDF2 hashing.
src/SaasPos.Api             REST API, JWT auth, tenant resolution from token.
src/SaasPos.Web             Blazor UI: login, POS, products, inventory, sales, dashboard.
tests/SaasPos.Tests         Domain unit tests.
```

## Multi-tenancy
Shared database; every business row carries `TenantId`. EF global query filters scope all reads to the
tenant in the caller's JWT, and `SaveChanges` rejects cross-tenant writes.

## Run
```
dotnet run --project src/SaasPos.Api   # http://localhost:5094 (dev seeds tenant "demo", admin / Admin123!)
dotnet run --project src/SaasPos.Web   # http://localhost:5034
```
Onboard a client: `POST /api/platform/tenants` with header `X-Provisioning-Key`.

## Production notes
Set `Jwt:Key` (32+ chars) and `Provisioning:Key` via env vars/secrets; switch `ConnectionStrings:Default`
to a production DB (swap the EF provider) and use migrations instead of `EnsureCreated`.

