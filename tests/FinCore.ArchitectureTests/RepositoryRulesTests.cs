using System.Diagnostics;

namespace FinCore.ArchitectureTests;

/// <summary>AC-4: the commit-msg hook rejects AI attribution lines (spec §35.7.1).</summary>
public sealed class RepositoryRulesTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FinCore.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static int RunHook(string message)
    {
        var file = Path.GetTempFileName();
        File.WriteAllText(file, message);
        try
        {
            using var process = Process.Start(new ProcessStartInfo("bash", [Path.Combine(RepoRoot(), ".githooks", "commit-msg"), file])
            {
                RedirectStandardError = true,
            })!;
            process.WaitForExit();
            return process.ExitCode;
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("feat(org): add institutions\n\nCo-Authored-By: Claude <noreply@anthropic.com>")]
    [InlineData("fix(org): x\n\nco-authored-by: GitHub Copilot <x@y>")]
    [InlineData("chore: x\n\n🤖 Generated with Claude Code")]
    public void Hook_rejects_ai_attribution(string message) => RunHook(message).ShouldNotBe(0);

    [Fact]
    public void Hook_accepts_normal_messages() =>
        RunHook("feat(org): add institutions\n\nWhat changed:\n- Institutions can be created.\n\nCo-Authored-By: Jane Doe <jane@example.com>").ShouldBe(0);
}
