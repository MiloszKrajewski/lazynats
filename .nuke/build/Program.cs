using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.ChangeLog;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.Docker;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tools.Docker.DockerTasks;

// ReSharper disable UnusedMember.Local

[GitHubActions(
	"continuous",
	GitHubActionsImage.WindowsLatest,
	On = [GitHubActionsTrigger.Push],
	InvokedTargets = [nameof(Release)],
	CacheKeyFiles = ["Directory.Packages.props", "**/*.csproj"])]
class Program: NukeBuild
{
	public static int Main() => Execute<Program>(x => x.Build);

	[Parameter("Use debug configuration")] readonly bool Debug;

	Configuration Configuration =>
		!IsLocalBuild ? Configuration.Release :
		Debug ? Configuration.Debug :
		Configuration.Release;
	
	bool IsReleasing => 
		ScheduledTargets.Contains(Release) ||
		RunningTargets.Contains(Release) ||
		FinishedTargets.Contains(Release);

	[Solution] readonly Solution Solution;
	
	[GitRepository] readonly GitRepository GitRepository;
	[GitVersion] readonly GitVersion GitVersion;
	
	static readonly AbsolutePath NukeDirectory = RootDirectory / ".nuke";
	static readonly AbsolutePath OutputDirectory = RootDirectory / ".output";
	static readonly AbsolutePath DockerDirectory = RootDirectory / "docker";

	AbsolutePath PackageArtifactsPattern => OutputDirectory / $"*.{PackageVersion}.nupkg";

	readonly ReleaseNotes[] ReleaseNotes = ChangelogTasks
		.ReadReleaseNotes(RootDirectory / "CHANGES.md")
		.ToArray();

	NuGetVersion PackageVersion =>
		ReleaseNotes.FirstOrDefault()?.Version ??
		throw new ArgumentException("No release notes found");

	static bool IsNugetPackage(Project project) =>
		project.GetProperty<bool>("IsPackable");

	static bool IsApplication(Project project) =>
		project.GetOutputType() == "Exe" &&
		!IsTest(project);

	static bool IsTest(Project project) =>
		project.Name.EndsWith(".Tests") ||
		project.HasPackageReference("Microsoft.NET.Test.Sdk");

	IEnumerable<Project> Projects(Func<Project, bool> predicate = null) =>
		from p in Solution.AllProjects
		where !NukeDirectory.Contains(p)
		where predicate is null || predicate(p)
		select p;

	static void RemoveDebugSymbols(AbsolutePath publishDirectory) =>
		publishDirectory.GlobFiles("*.pdb", "*.dbg").ForEach(f => File.Delete(f));

	static void CompressToFresh(AbsolutePath sourceDirectory, AbsolutePath zipFile)
	{
		if (File.Exists(zipFile)) File.Delete(zipFile);
		sourceDirectory.CompressTo(zipFile);
	}

    static void RestoreSecretFile(string secretFile, string exampleFile)
	{
		if (File.Exists(RootDirectory / secretFile))
			return;

		Log.Warning(
			"Secret file '{SecretFile}' not found, copying example file '{ExampleFile}' instead",
			secretFile, exampleFile);
		(RootDirectory / exampleFile).Copy(RootDirectory / secretFile);
	}
    
    static string GetNugetApiKey() =>
	    EnvironmentInfo.GetVariable<string>("NUGET_API_KEY").NullIfEmpty() ??
	    throw new Exception("NUGET_API_KEY is not set");

    static string GetGitHubApiKey() =>
	    EnvironmentInfo.GetVariable<string>("GITHUB_API_KEY").NullIfEmpty() ??
	    throw new Exception("GITHUB_API_KEY is not set");

	Target Clean => _ => _
		.Before(Restore)
		.Executes(() =>
		{
			RootDirectory
				.GlobDirectories("**/bin", "**/obj", "packages")
				.Where(p => !NukeDirectory.Contains(p))
				.ForEach(f => f.DeleteDirectory());
			OutputDirectory.CreateOrCleanDirectory();
		});

	Target Restore => _ => _
		.After(Clean)
		.Executes(() =>
		{
            RestoreSecretFile(".secrets.cfg", "res/.secrets.example.cfg");
            RestoreSecretFile(".signing.snk", "res/.signing.example.snk");

			DotNetToolRestore();
			DotNetRestore(s => s.SetProjectFile(Solution));
		});

	Target Build => _ => _
		.DependsOn(Restore)
		.Executes(() =>
		{
			DotNetBuild(s => s
				.SetProjectFile(Solution)
				.SetConfiguration(Configuration)
				.SetProperty("IsReleasing", IsReleasing)
				.SetVersion(PackageVersion.ToString())
				.EnableNoRestore());
		});

	Target Rebuild => _ => _
		.DependsOn(Build).DependsOn(Clean)
		.Executes(() => { });

	Target Release => _ => _
		.DependsOn(Rebuild)
		.Produces(OutputDirectory / "*.nupkg")
		.Executes(() =>
		{
			foreach (var p in Projects(IsNugetPackage))
			{
			DotNetPack(s => s
				.SetProject(p)
				.SetConfiguration(Configuration)
				.SetVersion(PackageVersion.ToString())
				.SetOutputDirectory(OutputDirectory)
				.EnableNoRestore()
				.EnableNoBuild()
			);
			}

			foreach (var a in Projects(IsApplication))
			{
				DotNetPublish(s => s
					.SetProject(a.Path)
					.SetConfiguration(Configuration.Release)
					.SetOutput(OutputDirectory / a.Name)
				);
				RemoveDebugSymbols(OutputDirectory / a.Name);
				var zipName = $"{a.Name}-{PackageVersion}.zip";
				Log.Information("Compressing {Application}...", zipName);
				CompressToFresh(OutputDirectory / a.Name, OutputDirectory / zipName);
			}
		});
	
	Target ReleaseDocker => _ => _
		.DependsOn(Release)
		.Executes(() =>
		{
			var dockerTools = new DockerTools(DockerDirectory);
			
			foreach (var a in Projects(IsApplication))
			{
				var dockerFile = dockerTools.FindDockerFile(a);
				if (dockerFile is null) continue;

				var imageName = dockerTools.GetDockerImageName(a);
				var artifactsPath = RootDirectory.GetUnixRelativePathTo(OutputDirectory / a.Name);
				DockerBuild(s => s
					.SetProcessWorkingDirectory(RootDirectory)
					.SetPath(RootDirectory)
					.SetFile(dockerFile)
					.AddBuildArg($"PROJECT_NAME={a.Name}")
					.AddBuildArg($"PROJECT_PATH={artifactsPath}")
					.AddTag($"{imageName}:{PackageVersion}")
					.AddTag($"{imageName}:latest")
					.EnableQuiet()
				);
			}
		});

	Target ReleaseWindowsX64 => _ => _
		.DependsOn(Restore)
		.Executes(() =>
		{
			if (!OperatingSystem.IsWindows())
				throw new PlatformNotSupportedException(
					"release-windows-x64 requires a Windows host: Native AOT for win-x64 has no " +
					"cross-compilation story from Linux or macOS.");

			var project = Projects(IsApplication).Single();
			var publishDirectory = OutputDirectory / $"{project.Name}-win-x64";

			DotNetPublish(s => s
				.SetProject(project.Path)
				.SetConfiguration(Configuration.Release)
				.SetRuntime("win-x64")
				.EnableSelfContained()
				.SetProperty("PublishAot", true)
				.SetOutput(publishDirectory));
			RemoveDebugSymbols(publishDirectory);

			var zipName = $"{project.Name}-{PackageVersion}-windows-x64.zip";
			Log.Information("Compressing {ZipName}...", zipName);
			CompressToFresh(publishDirectory, OutputDirectory / zipName);
		});

	Target ReleaseLinuxX64 => _ => _
		.DependsOn(Restore)
		.Executes(() =>
		{
			var project = Projects(IsApplication).Single();
			var builderImage = "lazynats-release-linux-x64-builder";
			var publishDirectory = OutputDirectory / $"{project.Name}-linux-x64";
			publishDirectory.CreateOrCleanDirectory();

			DockerBuild(s => s
				.SetProcessWorkingDirectory(RootDirectory)
				.SetPath(RootDirectory)
				.SetFile(DockerDirectory / "release-linux-x64.dockerfile")
				.AddTag(builderImage)
				.EnableQuiet());

			var projectPath = RootDirectory.GetUnixRelativePathTo(project.Path);
			DockerRun(s => s
				.SetProcessWorkingDirectory(RootDirectory)
				.SetImage(builderImage)
				.EnableRm()
				.SetVolume($"{RootDirectory}:/repo", $"{publishDirectory}:/out")
				.SetWorkdir("/repo")
				.SetCommand("dotnet")
				.SetArgs(
					"publish", $"/repo/{projectPath}",
					"--configuration", Configuration.Release,
					"--runtime", "linux-x64",
					"--self-contained",
					"-p:PublishAot=true",
					"--output", "/out"));
			RemoveDebugSymbols(publishDirectory);

			var zipName = $"{project.Name}-{PackageVersion}-linux-x64.zip";
			Log.Information("Compressing {ZipName}...", zipName);
			CompressToFresh(publishDirectory, OutputDirectory / zipName);
		});

	Target ReleaseLinuxArm64 => _ => _
		.Executes(() =>
		{
			// Two possible future implementations, neither wired up yet:
			// (1) a QEMU-emulated `docker run --platform linux/arm64` container, mirroring
			//     release-linux-x64 but under emulation; or
			// (2) a portable clang/binutils-aarch64/sysroot cross-toolchain run natively.
			throw new NotSupportedException(
				"release-linux-arm64 is not implemented yet: it needs either a QEMU-emulated " +
				"linux/arm64 build container or a clang/binutils-aarch64/sysroot cross-toolchain, " +
				"neither of which exists in this pipeline yet.");
		});

	Target ReleaseMacosArm64 => _ => _
		.Executes(() =>
		{
			// Native AOT for macOS can only be produced on macOS hardware - there is no
			// cross-compile or container-based path from Windows/Linux.
			throw new NotSupportedException(
				"release-macos-arm64 is not implemented: a macOS Native AOT build requires an " +
				"actual macOS build host, which is not available to this pipeline.");
		});

	Target VerifyArtifacts => _ => _
		.After(Release)
		.Executes(() =>
		{
			if (!PackageArtifactsPattern.GlobFiles().Any())
				throw new FileNotFoundException($"No artifacts found for {PackageArtifactsPattern}");
		});

	Target PublishToNuget => _ => _
		.After(Release).After(PublishToGitHub)
		.DependsOn(VerifyArtifacts)
		.Executes(() =>
		{
			var token = GetNugetApiKey();

			DotNetNuGetPush(s => s
				.SetTargetPath(PackageArtifactsPattern)
				.SetSource("https://api.nuget.org/v3/index.json")
				.EnableSkipDuplicate()
				.SetApiKey(token));
		});

	Target PublishToGitHub => _ => _
		.After(Release)
		.After(ReleaseWindowsX64)
		.After(ReleaseLinuxX64)
		.After(ReleaseLinuxArm64)
		.After(ReleaseMacosArm64)
		.DependsOn(VerifyArtifacts)
		.Executes(async () =>
		{
			var token = GetGitHubApiKey();
			var artifacts = PackageArtifactsPattern.GlobFiles().ToArray();
			var api = new GitHubApi(token);
			await api.Release(
				PackageVersion,
				GitRepository,
				GitVersion,
				ReleaseNotes.First(),
				artifacts);
		});

	Target Test => _ => _
		.After(Build)
		.Executes(() =>
		{
			foreach (var p in Projects(IsTest))
			{
				DotNetTest(s => s
					.SetProjectFile(p)
					.SetConfiguration(Configuration)
				);
			}
		});
}
