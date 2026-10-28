using System.Text;

namespace Not.Test.Tests;

/// <summary>What the rules are given (#647): the projects as their files say them, found where the product keeps them and nowhere else.</summary>
public sealed class ProjectGraphTests : IDisposable
{
    static readonly string[] FOLDERS = ["src", "nugets", "tests", "tools"];

    readonly string _folder = Path.Combine(AppContext.BaseDirectory, "projects-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void The_projects_are_the_ones_in_the_folders_of_the_product_and_not_the_ones_in_its_build_output_or_scratch()
    {
        foreach (
            var path in new[]
            {
                "src/A/A.csproj",
                "nugets/B/B.csproj",
                "tests/C/C.csproj",
                "tools/D/D.csproj",
                "src/A/bin/Debug/BinGhost.csproj",
                "src/A/obj/ObjGhost.csproj",
                "src/A/node_modules/x/NodeGhost.csproj",
                "scratch/E/E.csproj",
                ".tmp/F/F.csproj",
            }
        )
        {
            Write(path, "<Project />");
        }

        Assert.Equal(["A", "B", "C", "D"], ProjectGraph.Load(_folder, FOLDERS).Projects.Select(x => x.Name).Order());
    }

    [Fact]
    public void A_repository_that_lacks_some_of_the_folders_is_read_all_the_same()
    {
        Write("src/A/A.csproj", "<Project />");

        Assert.Equal(["A"], ProjectGraph.Load(_folder, FOLDERS).Projects.Select(x => x.Name));
    }

    [Fact]
    public void A_project_is_at_its_path_from_the_root_with_forward_slashes()
    {
        Write("src/Domain/A/A.csproj", "<Project />");

        Assert.Equal("src/Domain/A/A.csproj", ProjectGraph.Load(_folder, FOLDERS).Get("A").Path);
    }

    [Fact]
    public void A_project_file_with_a_byte_order_mark_is_read()
    {
        File.WriteAllBytes(
            Prepare("src/A/A.csproj"),
            [
                .. new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble(),
                .. "<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"u8.ToArray(),
            ]
        );

        Assert.Equal(["net10.0"], ProjectGraph.Load(_folder, FOLDERS).Get("A").Targets);
    }

    [Fact]
    public void A_project_that_names_its_targets_in_a_list_is_read_by_all_of_them()
    {
        var graph = ProjectGraph.Of(
            (
                "src/Both/Both.csproj",
                "<Project><PropertyGroup><TargetFrameworks> net10.0 ; net8.0 ;</TargetFrameworks></PropertyGroup></Project>"
            )
        );

        Assert.Equal(["net10.0", "net8.0"], graph.Get("Both").Targets);
    }

    [Fact]
    public void A_project_reads_its_references_whatever_the_slash()
    {
        var graph = ProjectGraph.Of(
            (
                "src/A/A.csproj",
                """<Project><ItemGroup><ProjectReference Include="..\B\B.csproj" /><ProjectReference Include="../C/C.csproj" /></ItemGroup></Project>"""
            ),
            ("src/B/B.csproj", "<Project />"),
            ("src/C/C.csproj", "<Project />")
        );

        Assert.Equal(["B", "C"], graph.Get("A").Projects);
    }

    [Fact]
    public void A_project_takes_the_packages_it_includes_and_not_the_ones_it_changes()
    {
        var graph = ProjectGraph.Of(
            (
                "src/A/A.csproj",
                """<Project><ItemGroup><PackageReference Include="Some.Package" Version="1.2.3" /><PackageReference Update="Other.Package" Version="4" /></ItemGroup></Project>"""
            )
        );

        Assert.Equal(["Some.Package"], graph.Get("A").Packages);
    }

    [Fact]
    public void A_project_is_the_closure_of_itself_and_every_project_it_reaches()
    {
        var graph = ProjectGraph.Of(
            ProjectOf("A", "B", "D"),
            ProjectOf("B", "C"),
            ProjectOf("C"),
            ProjectOf("D", "C"),
            ProjectOf("E", "A")
        );

        Assert.Equal(["A", "B", "C", "D"], graph.Closure("A").Select(x => x.Name).Order());
        Assert.Equal(["B", "C"], graph.Closure("B").Select(x => x.Name).Order());
    }

    [Fact]
    public void Projects_that_reference_each_other_are_followed_once()
    {
        var graph = ProjectGraph.Of(ProjectOf("A", "B"), ProjectOf("B", "A"));

        Assert.Equal(["A", "B"], graph.Closure("A").Select(x => x.Name).Order());
    }

    [Fact]
    public void A_project_that_the_repository_lacks_is_left_out_of_the_closure()
    {
        var graph = ProjectGraph.Of(ProjectOf("A", "Missing"));

        Assert.Equal(["A"], graph.Closure("A").Select(x => x.Name));
        Assert.False(graph.Has("Missing"));
        Assert.Throws<InvalidOperationException>(() => graph.Get("Missing"));
    }

    static (string Path, string Xml) ProjectOf(string name, params string[] references)
    {
        var items = string.Concat(references.Select(x => $"""<ProjectReference Include="..\{x}\{x}.csproj" />"""));
        return ($"src/{name}/{name}.csproj", $"<Project><ItemGroup>{items}</ItemGroup></Project>");
    }

    void Write(string path, string text)
    {
        File.WriteAllText(Prepare(path), text);
    }

    string Prepare(string path)
    {
        var file = Path.Combine(_folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        return file;
    }
}
