namespace FinCore.BuildingBlocks.Application.Security;

/// <summary>Names of the modules switched on in this environment (spec §33.7.4); the clients hide the rest.</summary>
public interface IEnabledModules
{
    IReadOnlyList<string> Names { get; }
}
