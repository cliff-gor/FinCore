using FinCore.Modules.Organization.Domain.Errors;
using FinCore.Modules.Organization.Domain.Institutions;
using FinCore.SharedKernel.Domain;
using FinCore.SharedKernel.Results;

namespace FinCore.Modules.Organization.Domain.Branches;

/// <summary>
/// A branch of an institution (spec §12, spec 002). The hierarchy is deliberately shallow: a branch may report to a
/// parent, but a branch that has a parent cannot be a parent itself (max depth 2). That covers regional head offices
/// without the cycle and depth problems of an arbitrary tree.
/// </summary>
public sealed class Branch : AggregateRoot<BranchId>
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 100;
    public const int AddressMaxLength = 300;

    private Branch(BranchId id, InstitutionId institutionId, BranchCode code, string name, string? address, BranchId? parentBranchId)
        : base(id)
    {
        InstitutionId = institutionId;
        Code = code;
        Name = name;
        Address = address;
        ParentBranchId = parentBranchId;
        Status = BranchStatus.Active;
    }

    /// <summary>EF Core materialization only.</summary>
    private Branch()
    {
        Code = null!;
        Name = null!;
    }

    public InstitutionId InstitutionId { get; private set; }

    /// <summary>Immutable: it is embedded in account numbers (spec §15).</summary>
    public BranchCode Code { get; private set; }

    public string Name { get; private set; }

    /// <summary>Free text until Customers (M2) introduces structured addresses.</summary>
    public string? Address { get; private set; }

    public BranchId? ParentBranchId { get; private set; }

    public BranchStatus Status { get; private set; }

    public static Result<Branch> Create(
        InstitutionId institutionId, string? code, string? name, string? address, Branch? parent, DateTime utcNow)
    {
        var codeResult = BranchCode.Create(code);
        if (codeResult.IsFailure)
        {
            return codeResult.Error;
        }

        var details = ValidateDetails(name, address);
        if (details.IsFailure)
        {
            return details.Error;
        }

        var parentCheck = ValidateParent(institutionId, parent, self: null, selfHasChildren: false);
        if (parentCheck.IsFailure)
        {
            return parentCheck.Error;
        }

        var branch = new Branch(BranchId.New(), institutionId, codeResult.Value, details.Value.Name, details.Value.Address, parent?.Id);
        branch.Raise(new BranchCreatedDomainEvent(Guid.CreateVersion7(), utcNow, branch.Id, institutionId, branch.Code.Value, branch.Name, branch.ParentBranchId));
        return branch;
    }

    /// <summary>Changes name, address and parent; the code never changes.</summary>
    /// <param name="hasChildren">Whether other branches report to this one; such a branch cannot get a parent.</param>
    public Result Update(string? name, string? address, Branch? parent, bool hasChildren)
    {
        if (Status == BranchStatus.Closed)
        {
            return OrganizationErrors.BranchClosed;
        }

        var details = ValidateDetails(name, address);
        if (details.IsFailure)
        {
            return details.Error;
        }

        var parentCheck = ValidateParent(InstitutionId, parent, this, hasChildren);
        if (parentCheck.IsFailure)
        {
            return parentCheck.Error;
        }

        Name = details.Value.Name;
        Address = details.Value.Address;
        ParentBranchId = parent?.Id;
        return Result.Success();
    }

    /// <summary>
    /// Closes the branch. Not allowed while active staff have it as their home branch (spec 002): they would be left
    /// without a branch scope.
    /// </summary>
    public Result Close(bool hasActiveStaff, bool hasActiveChildren, DateTime utcNow)
    {
        if (Status == BranchStatus.Closed)
        {
            return OrganizationErrors.BranchClosed;
        }

        if (hasActiveStaff)
        {
            return OrganizationErrors.BranchHasActiveStaff;
        }

        // Closing a parent first would leave its children reporting to a closed branch.
        if (hasActiveChildren)
        {
            return OrganizationErrors.BranchHasActiveChildren;
        }

        Status = BranchStatus.Closed;
        Raise(new BranchClosedDomainEvent(Guid.CreateVersion7(), utcNow, Id, InstitutionId));
        return Result.Success();
    }

    private static Result<(string Name, string? Address)> ValidateDetails(string? name, string? address)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length is < NameMinLength or > NameMaxLength)
        {
            return OrganizationErrors.InvalidBranchName;
        }

        var trimmedAddress = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        if (trimmedAddress is { Length: > AddressMaxLength })
        {
            return OrganizationErrors.InvalidBranchAddress;
        }

        return (trimmedName, trimmedAddress);
    }

    private static Result ValidateParent(InstitutionId institutionId, Branch? parent, Branch? self, bool selfHasChildren)
    {
        if (parent is null)
        {
            return Result.Success();
        }

        // Each failure below is the same field error: the chosen parent is not allowed.
        var invalid = parent.InstitutionId != institutionId // never across institutions
            || parent.ParentBranchId is not null            // parent would be at depth 2 → child at depth 3
            || parent.Status != BranchStatus.Active
            || (self is not null && (parent.Id == self.Id || selfHasChildren)); // self-parenting or a parent gaining a parent

        return invalid ? OrganizationErrors.InvalidParentBranch : Result.Success();
    }
}
