namespace SaasPos.Domain.Common;

public class DomainException(string message) : Exception(message);

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; protected set; } = DateTime.UtcNow;
}

/// <summary>Every business entity belongs to exactly one tenant (client).</summary>
public abstract class TenantEntity : Entity
{
    public Guid TenantId { get; protected set; }

    protected TenantEntity() { }
    protected TenantEntity(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new DomainException("TenantId is required.");
        TenantId = tenantId;
    }
}
