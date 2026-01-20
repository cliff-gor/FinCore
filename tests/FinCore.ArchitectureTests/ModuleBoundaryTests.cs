using NetArchTest.Rules;

namespace FinCore.ArchitectureTests;

/// <summary>Enforces spec §6 dependency rules (AC-3).</summary>
public sealed class ModuleBoundaryTests
{
    public static TheoryData<string> Modules => ModuleAssemblies.ModuleData();

    [Fact]
    public void Every_module_has_all_five_projects()
    {
        ModuleAssemblies.ModuleNames.ShouldNotBeEmpty();
        foreach (var module in ModuleAssemblies.ModuleNames)
        {
            foreach (var layer in ModuleAssemblies.Layers)
            {
                ModuleAssemblies.Get(module, layer).ShouldNotBeNull($"{module}.{layer} is missing");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Domain_depends_only_on_shared_kernel(string module)
    {
        var result = Types.InAssembly(ModuleAssemblies.Get(module, "Domain")!)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql", "FluentValidation", "Quartz",
                "FinCore.BuildingBlocks", $"FinCore.Modules.{module}.Application", $"FinCore.Modules.{module}.Infrastructure",
                $"FinCore.Modules.{module}.Presentation", $"FinCore.Modules.{module}.Contracts")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Contracts_depend_only_on_shared_kernel(string module)
    {
        var result = Types.InAssembly(ModuleAssemblies.Get(module, "Contracts")!)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "FinCore.BuildingBlocks",
                $"FinCore.Modules.{module}.Domain", $"FinCore.Modules.{module}.Application",
                $"FinCore.Modules.{module}.Infrastructure", $"FinCore.Modules.{module}.Presentation")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Application_does_not_depend_on_infrastructure_presentation_or_frameworks(string module)
    {
        var result = Types.InAssembly(ModuleAssemblies.Get(module, "Application")!)
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql",
                $"FinCore.Modules.{module}.Infrastructure", $"FinCore.Modules.{module}.Presentation")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Modules_reference_other_modules_only_through_contracts(string module)
    {
        var forbidden = ModuleAssemblies.ModuleNames
            .Where(other => other != module)
            .SelectMany(other => new[] { "Domain", "Application", "Infrastructure", "Presentation" }
                .Select(layer => $"FinCore.Modules.{other}.{layer}"))
            .ToArray();
        if (forbidden.Length == 0)
        {
            return; // Only one module exists so far; the rule activates when the second module lands.
        }

        foreach (var layer in ModuleAssemblies.Layers)
        {
            var result = Types.InAssembly(ModuleAssemblies.Get(module, layer)!)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();
            result.IsSuccessful.ShouldBeTrue(Failing(result));
        }
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Handlers_and_validators_are_internal_sealed(string module)
    {
        var types = Types.InAssembly(ModuleAssemblies.Get(module, "Application")!)
            .That().HaveNameEndingWith("Handler").Or().HaveNameEndingWith("Validator")
            .GetTypes()
            .ToList();

        types.ShouldNotBeEmpty();
        types.ShouldAllBe(t => t.IsSealed && !t.IsPublic && !t.IsNestedPublic);
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Entity_configurations_are_internal_sealed(string module)
    {
        var types = Types.InAssembly(ModuleAssemblies.Get(module, "Infrastructure")!)
            .That().HaveNameEndingWith("Configuration")
            .GetTypes()
            .ToList();

        types.ShouldAllBe(t => t.IsSealed && !t.IsPublic);
    }

    [Fact]
    public void Shared_kernel_has_no_framework_dependencies()
    {
        var result = Types.InAssembly(typeof(FinCore.SharedKernel.Money.Money).Assembly)
            .ShouldNot().HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions", "FinCore.BuildingBlocks")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Failing(result));
    }

    private static string Failing(NetArchTest.Rules.TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
