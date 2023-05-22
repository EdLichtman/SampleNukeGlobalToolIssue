using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Build.Evaluation;
using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.PowerShell;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.PowerShell.PowerShellTasks;
using Project = Microsoft.Build.Evaluation.Project;


class Build : NukeBuild
{
    [CanBeNull] 
    AbsolutePath Solution => RootDirectory.GlobFiles("SampleNukeIssue.sln").FirstOrDefault();
    AbsolutePath InternalLibrary => RootDirectory / "InternalLibrary" / "InternalLibrary.csproj";

    AbsolutePath GlobalTool => RootDirectory / "GlobalTool" / "_build.csproj";

    AbsolutePath ToggleIssue => RootDirectory / "ToggleIssue" / "ToggleIssue.csproj";
    AbsolutePath Demonstration => RootDirectory / "Demonstration";
    
    enum Behavior
    {
        Working,
        NotWorking
    }

    public static int Main () => Execute<Build>(x => x.Toggle);


    [Parameter("The expected behavior to exhibit. Either 'NotWorking', or 'Working'")]
    readonly Behavior? ExpectedBehavior;

    Target Toggle => _ => _
        .Description("Toggles whether or not nuke tooling has been packed. Requires --root to be the (sln)\\ToggleIssue\\ directory.")
        .OnlyWhenDynamic(() => Solution != null)
        .Requires(() => ExpectedBehavior)
        .DependsOn(
            UpdateGlobalTool, 
            CompileNuGetPackages,
            UpdateToolManifest,
            Reproduce);

    Target UpdateGlobalTool => _ => _
        .Unlisted()
        .Before(CompileNuGetPackages)
        .Executes(() =>
        {
            var project = ProjectModelTasks.ParseProject(GlobalTool);
            var version = NuGetVersion.Parse(project.GetPropertyValue("Version"));
            var newVersion = new NuGetVersion(version.Major, version.Minor, version.Patch + 1);
            project.SetProperty("Version", newVersion.ToString());

            if (ExpectedBehavior == Behavior.NotWorking)
            {
                RemoveNukeFromGlobalTool(project);
            }
            else
            {
                AddNukeToGlobalTool(project);
            }

            project.Save();
        });

    private void RemoveNukeFromGlobalTool(Project project)
    {
        var nukePackageReference = GetNukePackageReference(project);
        if (nukePackageReference != null)
        {
            project.RemoveItem(nukePackageReference);
        }
    }

    private void AddNukeToGlobalTool(Project project)
    {
        var nukePackageReference = GetNukePackageReference(project);
        if (nukePackageReference == null)
        {
            project.AddItem("PackageReference", "Nuke.Common", new[]
            {
                new KeyValuePair<string, string>("Version", "*")
            });
        }
    }

    [CanBeNull]
    private ProjectItem GetNukePackageReference(Project project)
    {
        return project.GetItems("PackageReference").FirstOrDefault(x => x.EvaluatedInclude == "Nuke.Common");
    }

    Target CompileNuGetPackages => _ => _
        .Unlisted()
        .Before(UpdateToolManifest)
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(InternalLibrary));
            DotNetPack(s => s.SetProject(InternalLibrary));

            DotNetRestore(s => s.SetProjectFile(GlobalTool));
            DotNetPack(s => s.SetProject(GlobalTool));
        });

    Target UpdateToolManifest => _ => _
        .Unlisted()
        .Before(Reproduce)
        .Executes(() =>
        {
            PowerShell(s => s.SetCommand($"cd '{Demonstration}';dotnet tool update SampleNukeTool;dotnet tool restore;"));
        });

    Target Reproduce => _ => _
        .Unlisted()
        .Executes(() =>
        {
            PowerShell(s => s.SetCommand($"cd '{Demonstration}';dotnet sample-nuke-tool;"));
            // var whatToSee = ExpectedBehavior == Behavior.NotWorking
            //     ? "an exception showing that the PackageDownload was not shipped with the nupkg."
            //     : "no exception at all. It will work as expected.";
            // Log.Information(
            //     $"Now, go to '{ToggleIssue.Parent}' and run 'dotnet sample-nuke-tool'. You will see {whatToSee}");
        });

}
