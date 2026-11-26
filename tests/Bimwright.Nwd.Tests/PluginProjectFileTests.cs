using System;
using System.IO;
using System.Xml.Linq;

namespace Bimwright.Nwd.Tests;

public sealed class PluginProjectFileTests
{
    private static readonly int[] Years = { 2022, 2023, 2024, 2025, 2026, 2027 };

    [Fact]
    public void VerifyPluginProjectsShape()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var srcDir = Path.Combine(dir!.FullName, "src");

        foreach (var year in Years)
        {
            var folder = "plugin-navis" + (year % 100);
            var file = $"Bimwright.Nwd.Plugin.Navis{year % 100}.csproj";
            var csprojPath = Path.Combine(srcDir, folder, file);

            Assert.True(File.Exists(csprojPath), $"Project file not found: {csprojPath}");

            var doc = XDocument.Load(csprojPath);
            var root = doc.Root;
            Assert.NotNull(root);

            var propertyGroup = root.Element("PropertyGroup");
            Assert.NotNull(propertyGroup);

            var targetFramework = propertyGroup.Element("TargetFramework")?.Value;
            Assert.Equal("net48", targetFramework);

            var rootNamespace = propertyGroup.Element("RootNamespace")?.Value;
            Assert.Equal("Bimwright.Nwd.Plugin", rootNamespace);

            var assemblyName = propertyGroup.Element("AssemblyName")?.Value;
            Assert.Equal($"Bimwright.Nwd.Plugin.Navis{year % 100}", assemblyName);

            // Runtime health must observe real SDK-generated version/revision attributes.
            Assert.Equal("true", propertyGroup.Element("GenerateAssemblyInfo")?.Value);

            var defineConstants = propertyGroup.Element("DefineConstants")?.Value;
            Assert.NotNull(defineConstants);
            Assert.Contains($"NAVIS{year}", defineConstants);
            Assert.StartsWith("$(DefineConstants);", defineConstants); // must append

            // Navisworks API: that year's reference package, compile-time only (never copied to the output or bundle).
            var apiVersion = propertyGroup.Element("NavisworksApiPackageVersion")?.Value;
            Assert.NotNull(apiVersion);
            Assert.StartsWith($"{year}.", apiVersion);
            var itemGroups = root.Elements("ItemGroup").ToArray();
            var packages = itemGroups.SelectMany(ig => ig.Elements("PackageReference")).ToArray();
            var api = packages.SingleOrDefault(p => p.Attribute("Include")?.Value == "Speckle.Navisworks.API");
            Assert.NotNull(api);
            Assert.Equal("$(NavisworksApiPackageVersion)", api!.Attribute("Version")?.Value);
            Assert.Equal("runtime", api.Attribute("ExcludeAssets")?.Value);
            Assert.Equal("all", api.Attribute("PrivateAssets")?.Value);
            // record/init on net48 needs the IsExternalInit polyfill in every shell.
            Assert.Contains(packages, p => p.Attribute("Include")?.Value == "IsExternalInit");
            // No install-path references: every shell builds without Navisworks installed
            // (only the net48 BCL System.Net.Http reference without HintPath is allowed).
            var refs = itemGroups.SelectMany(ig => ig.Elements("Reference")).ToArray();
            Assert.All(refs, r =>
            {
                Assert.Null(r.Element("HintPath"));
                Assert.Equal("System.Net.Http", r.Attribute("Include")?.Value);
            });
            Assert.Null(propertyGroup.Element("NavisworksInstallDir"));

            // no net8/net10 TFA
            var allText = File.ReadAllText(csprojPath);
            Assert.DoesNotContain("net8.0-windows", allText);
            Assert.DoesNotContain("net10.0-windows", allText);

            // shared linkage
            var compile = itemGroups.SelectMany(ig => ig.Elements("Compile")).FirstOrDefault();
            Assert.NotNull(compile);
            Assert.Equal(@"..\shared\**\*.cs", compile.Attribute("Include")?.Value);
        }
    }
}
