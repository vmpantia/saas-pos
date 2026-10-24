using SaasPos.Domain.Common;
using SaasPos.Domain.Enums;

namespace SaasPos.Domain.Entities;

public class Tenant : Entity
{
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;

    private Tenant() { }

    public Tenant(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Tenant name is required.");
        if (string.IsNullOrWhiteSpace(code)) throw new DomainException("Tenant code is required.");
        Name = name.Trim();
        Code = code.Trim().ToLowerInvariant();
    }

    public void Deactivate() => IsActive = false;
}

public class User : TenantEntity
{
    public string Username { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string FullName { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;

    private User() { }

    public User(Guid tenantId, string username, string passwordHash, string fullName, UserRole role) : base(tenantId)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new DomainException("Username is required.");
        Username = username.Trim().ToLowerInvariant();
        PasswordHash = passwordHash;
        FullName = fullName;
        Role = role;
    }
}
