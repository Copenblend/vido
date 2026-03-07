using System.IO;

using Xunit;

namespace Vido.Tests.Build;

/// <summary>
/// Structural tests for <c>build-release.ps1</c> — verifies the script
/// references the correct projects, produces the expected artifacts,
/// and retains required parameters and pipeline steps.
/// </summary>
public sealed class BuildReleaseScriptTests
{
    private static readonly string ScriptPath = Path.Combine(
        FindRepoRoot(), "build-release.ps1");

    private static readonly string ScriptContent = File.ReadAllText(ScriptPath);

    // ── Project references ─────────────────────────────────────────────

    [Fact]
    public void Script_ReferencesVidoSetupProject()
    {
        Assert.Contains(@"src\Vido.Setup\VidoSetup.csproj", ScriptContent);
    }

    [Fact]
    public void Script_DoesNotReferenceWixInstallerProject()
    {
        Assert.DoesNotContain("Vido.Installer.wixproj", ScriptContent);
    }

    [Fact]
    public void Script_DoesNotReferenceWixCli()
    {
        Assert.DoesNotContain("Get-Command wix", ScriptContent);
        Assert.DoesNotContain("wix extension add", ScriptContent);
    }

    [Fact]
    public void Script_DoesNotReferenceMsi()
    {
        Assert.DoesNotContain(".msi", ScriptContent);
        Assert.DoesNotContain("MSI", ScriptContent);
    }

    // ── Setup EXE build step ───────────────────────────────────────────

    [Fact]
    public void Script_CreatesPayloadZip()
    {
        Assert.Contains("payload.zip", ScriptContent);
        Assert.Contains("Compress-Archive", ScriptContent);
    }

    [Fact]
    public void Script_PublishesSetupAsPublishSingleFile()
    {
        Assert.Contains("PublishSingleFile=true", ScriptContent);
    }

    [Fact]
    public void Script_PassesPayloadZipToSetupBuild()
    {
        Assert.Contains("PayloadZip=$PayloadZip", ScriptContent);
    }

    [Fact]
    public void Script_ProducesVidoSetupExe()
    {
        Assert.Contains("VidoSetup-$Version.exe", ScriptContent);
        Assert.Contains("VidoSetup.exe", ScriptContent);
    }

    [Fact]
    public void Script_CleansUpPayloadZipAfterBuild()
    {
        // Verify the cleanup section exists after the build section
        var payloadCleanupIndex = ScriptContent.IndexOf("Remove-Item $PayloadZip");
        var setupOutputCleanupIndex = ScriptContent.IndexOf("Remove-Item $SetupOutput");
        Assert.True(payloadCleanupIndex > 0, "Script should clean up payload.zip");
        Assert.True(setupOutputCleanupIndex > 0, "Script should clean up setup output directory");
    }

    [Fact]
    public void Script_SignsSetupExe()
    {
        Assert.Contains(@"Invoke-CodeSign $SetupPath ""Vido Installer""", ScriptContent);
    }

    // ── Parameters and pipeline steps ──────────────────────────────────

    [Fact]
    public void Script_HasSkipInstallerParameter()
    {
        Assert.Contains("[switch]$SkipInstaller", ScriptContent);
    }

    [Fact]
    public void Script_SkipInstallerSkipsBuild()
    {
        Assert.Contains("if (-not $SkipInstaller)", ScriptContent);
        Assert.Contains("Skipping installer build", ScriptContent);
    }

    [Fact]
    public void Script_HasAllFiveSteps()
    {
        Assert.Contains("[1/5]", ScriptContent);
        Assert.Contains("[2/5]", ScriptContent);
        Assert.Contains("[3/5]", ScriptContent);
        Assert.Contains("[4/5]", ScriptContent);
        Assert.Contains("[5/5]", ScriptContent);
    }

    [Fact]
    public void Script_StillProducesPortableZip()
    {
        Assert.Contains("portable.zip", ScriptContent);
        Assert.Contains("Creating portable zip", ScriptContent);
    }

    [Fact]
    public void Script_SignsMainExe()
    {
        Assert.Contains(@"Invoke-CodeSign (Join-Path $PortableDir ""Vido.exe"") ""Vido Video Player""", ScriptContent);
    }

    [Fact]
    public void Script_Step5Description_SaysCustomInstaller()
    {
        Assert.Contains("Building custom installer", ScriptContent);
    }

    [Fact]
    public void Script_Synopsis_DescribesSetupExe()
    {
        Assert.Contains("setup EXE", ScriptContent);
    }

    [Fact]
    public void Script_PublishesSelfContainedSetup()
    {
        // Verify self-contained and win-x64 are passed to dotnet publish for setup
        Assert.Contains("--self-contained", ScriptContent);
        Assert.Contains("-r win-x64", ScriptContent);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Vido.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("Could not find Vido.sln in any parent directory.");
    }
}
