namespace FinCore.SharedKernel.Domain;

/// <summary>Audit columns filled by the persistence layer (never by domain code).</summary>
public interface IAuditable
{
    DateTime CreatedAtUtc { get; }

    string CreatedBy { get; }

    DateTime? ModifiedAtUtc { get; }

    string? ModifiedBy { get; }

    void SetCreated(DateTime atUtc, string by);

    void SetModified(DateTime atUtc, string by);
}
