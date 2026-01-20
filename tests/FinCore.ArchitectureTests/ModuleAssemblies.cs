using System.Reflection;
using FinCore.Modules.Organization.Infrastructure;

namespace FinCore.ArchitectureTests;

/// <summary>Discovers every module's five assemblies from the host's references (add a module = it is covered).</summary>
internal static class ModuleAssemblies
{
    public static readonly string[] Layers = ["Domain", "Application", "Infrastructure", "Presentation", "Contracts"];

    static ModuleAssemblies()
    {
        // Force-load module assemblies referenced by the host.
        _ = typeof(OrganizationModule).Assembly;
        foreach (var name in typeof(Program).Assembly.GetReferencedAssemblies())
        {
            Assembly.Load(name);
        }

        All = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("FinCore.Modules.", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var assembly in All.ToList())
        {
            foreach (var reference in assembly.GetReferencedAssemblies()
                         .Where(r => r.Name!.StartsWith("FinCore.Modules.", StringComparison.Ordinal)))
            {
                if (All.All(a => a.GetName().Name != reference.Name))
                {
                    All.Add(Assembly.Load(reference));
                }
            }
        }
    }

    public static List<Assembly> All { get; }

    public static IEnumerable<string> ModuleNames =>
        All.Select(a => a.GetName().Name!.Split('.')[2]).Distinct(StringComparer.Ordinal);

    public static Assembly? Get(string module, string layer) =>
        All.SingleOrDefault(a => a.GetName().Name == $"FinCore.Modules.{module}.{layer}");

    public static TheoryData<string> ModuleData()
    {
        var data = new TheoryData<string>();
        foreach (var name in ModuleNames)
        {
            data.Add(name);
        }

        return data;
    }
}
