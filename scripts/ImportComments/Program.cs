// Import comments: for every C# file, list what each `using` namespace provides (types, and
// extension methods as Name()), note the game's global-namespace classes the file uses, and drop
// usings that provide nothing. Names come from the compiler (Roslyn) run on each project's real
// compile inputs, so they match the code.
//
// Usage: ImportComments (--write | --check) <project.json>...
//   project.json: output of `dotnet msbuild <csproj> -getItem:Compile -getItem:ReferencePath
//   -getProperty:DefineConstants -getProperty:LangVersion -getProperty:MSBuildProjectDirectory
//   -getProperty:IntermediateOutputPath` (see scripts/import-comments.sh).
//   --write rewrites the files; --check changes nothing and exits 1 if any file is out of date.
using System; // Console, StringComparer, StringComparison, StringSplitOptions
using System.Collections.Generic; // Dictionary, IEnumerable, List, SortedSet
using System.IO; // Directory, File, Path
// System.Linq: Any(), Contains(), Distinct(), FirstOrDefault(), Max(), OfType(),
//   OrderByDescending(), Select(), ToList(), Where()
using System.Linq;
using System.Text.Json; // JsonDocument, JsonElement
// Microsoft.CodeAnalysis: DiagnosticSeverity, IMethodSymbol, INamedTypeSymbol, INamespaceSymbol,
//   ISymbol, IsKind(), MetadataReference, MethodKind, OutputKind, SemanticModel,
//   SymbolEqualityComparer, SymbolInfo, SyntaxTree, TypeKind
using Microsoft.CodeAnalysis;
// Microsoft.CodeAnalysis.CSharp: CSharpCompilation, CSharpCompilationOptions, CSharpParseOptions,
//   CSharpSyntaxTree, GetSymbolInfo(), LanguageVersion, LanguageVersionFacts, SyntaxKind
using Microsoft.CodeAnalysis.CSharp;
// Microsoft.CodeAnalysis.CSharp.Syntax: CompilationUnitSyntax, MemberAccessExpressionSyntax,
//   QualifiedNameSyntax, SimpleNameSyntax, UsingDirectiveSyntax
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class Program
{
    private const int MaxLine = 100;
    private const string GameNote = "// Game classes used without a using";

    private static int Main(string[] args)
    {
        bool write = args.Contains("--write");
        bool check = args.Contains("--check");
        if (write == check)
        {
            Console.Error.WriteLine("Usage: ImportComments (--write | --check) <project.json>...");
            return 2;
        }

        var stale = new List<string>();
        int removed = 0;
        foreach (string json in args.Where(a => !a.StartsWith("--")))
        {
            if (!Run(json, write, stale, ref removed))
            {
                return 2;
            }
        }

        if (check && stale.Count > 0)
        {
            Console.Error.WriteLine("Import comments are out of date in:");
            foreach (string f in stale)
            {
                Console.Error.WriteLine("  " + f);
            }

            Console.Error.WriteLine("Run: npm run imports");
            return 1;
        }

        Console.WriteLine(
            (write ? "Import comments updated in " : "Import comments current; checked ")
                + (write ? stale.Count + " file(s)" : "all files")
                + (write ? ", " + removed + " unused using(s) removed" : "")
        );
        return 0;
    }

    private static bool Run(string jsonPath, bool write, List<string> stale, ref int removed)
    {
        JsonElement root = JsonDocument.Parse(File.ReadAllText(jsonPath)).RootElement;
        JsonElement items = root.GetProperty("Items");
        JsonElement props = root.GetProperty("Properties");
        string projectDir = props.GetProperty("MSBuildProjectDirectory").GetString();
        // MSBuild reports this with a Windows separator even on macOS ("obj\\Debug/net35/").
        string objDir = Path.Combine(
            projectDir,
            props.GetProperty("IntermediateOutputPath").GetString().Replace('\\', '/')
        );
        string[] defines = props
            .GetProperty("DefineConstants")
            .GetString()
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        LanguageVersion lang;
        LanguageVersionFacts.TryParse(props.GetProperty("LangVersion").GetString(), out lang);
        var options = new CSharpParseOptions(lang, preprocessorSymbols: defines);

        // Real sources plus files the build generates (BuildInfo.g.cs and the like): compiled for
        // name resolution, never edited.
        var paths = items
            .GetProperty("Compile")
            .EnumerateArray()
            .Select(e => Path.GetFullPath(e.GetProperty("FullPath").GetString()))
            .ToList();
        if (Directory.Exists(objDir))
        {
            paths.AddRange(Directory.GetFiles(objDir, "*.cs").Select(Path.GetFullPath));
        }

        paths = paths.Distinct().ToList();
        var trees = paths
            .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), options, p))
            .ToList();
        var references = items
            .GetProperty("ReferencePath")
            .EnumerateArray()
            .Select(e => MetadataReference.CreateFromFile(e.GetProperty("FullPath").GetString()))
            .ToList();
        CSharpCompilation compilation = CSharpCompilation.Create(
            "ImportComments",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
        );
        var errors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        if (errors.Count > 0)
        {
            // Unresolved names would be reported as unused: refuse rather than guess.
            Console.Error.WriteLine(
                Path.GetFileName(projectDir) + ": " + errors.Count + " compile error(s); first: " + errors[0]
            );
            Console.Error.WriteLine("Build the project first (scripts/import-comments.sh does).");
            return false;
        }

        foreach (SyntaxTree tree in trees.Where(t => !IsUnder(t.FilePath, objDir) && !t.FilePath.Contains("/obj/")))
        {
            string before = File.ReadAllText(tree.FilePath);
            int dropped;
            string after = Rewrite(compilation, tree, before, out dropped);
            if (after != before)
            {
                stale.Add(Path.GetRelativePath(Directory.GetCurrentDirectory(), tree.FilePath));
                removed += dropped;
                if (write)
                {
                    File.WriteAllText(tree.FilePath, after);
                }
            }
        }

        return true;
    }

    private static bool IsUnder(string path, string dir) =>
        path.StartsWith(Path.GetFullPath(dir), StringComparison.Ordinal);

    private static string Rewrite(CSharpCompilation compilation, SyntaxTree tree, string text, out int dropped)
    {
        dropped = 0;
        SemanticModel model = compilation.GetSemanticModel(tree);
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var usings = root
            .Usings.Where(u => u.Alias == null && u.StaticKeyword.IsKind(SyntaxKind.None))
            .ToList();
        if (usings.Count == 0)
        {
            return text;
        }

        var provided = new Dictionary<string, SortedSet<string>>();
        var game = new SortedSet<string>(StringComparer.Ordinal);
        void Add(string ns, string name)
        {
            if (!provided.TryGetValue(ns, out var set))
            {
                provided[ns] = set = new SortedSet<string>(StringComparer.Ordinal);
            }

            set.Add(name);
        }

        foreach (SimpleNameSyntax name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            if (name.Ancestors().OfType<UsingDirectiveSyntax>().Any())
            {
                continue;
            }

            // Qualified on the left (Ns.Type, x.Member): no using involved.
            if (name.Parent is QualifiedNameSyntax q && q.Right == name)
            {
                continue;
            }

            SymbolInfo info = model.GetSymbolInfo(name);
            ISymbol symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
            if (symbol is IMethodSymbol method)
            {
                if (method.MethodKind == MethodKind.ReducedExtension)
                {
                    IMethodSymbol ext = method.ReducedFrom ?? method;
                    Add(TopNamespace(ext.ContainingType).ToDisplayString(), method.Name + "()");
                    continue;
                }

                if (method.MethodKind == MethodKind.Constructor)
                {
                    symbol = method.ContainingType; // attributes and `new T()`
                }
            }

            if (name.Parent is MemberAccessExpressionSyntax access && access.Name == name)
            {
                continue;
            }

            if (symbol is INamedTypeSymbol type && type.TypeKind != TypeKind.Error && !type.IsTupleType)
            {
                INamespaceSymbol ns = TopNamespace(type);
                if (ns.IsGlobalNamespace)
                {
                    if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly))
                    {
                        game.Add(TopType(type).Name);
                    }
                }
                else
                {
                    Add(ns.ToDisplayString(), TopType(type).Name);
                }
            }
        }

        string newline = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Split(new[] { newline }, StringSplitOptions.None).ToList();
        int lastUsing = usings.Max(u => tree.GetLineSpan(u.Span).StartLinePosition.Line);

        // The game note sits under the usings (after an optional blank line): replace it.
        int noteAt = lastUsing + 1;
        if (noteAt < lines.Count && lines[noteAt].Trim().Length == 0 && noteAt + 1 < lines.Count
            && lines[noteAt + 1].TrimStart().StartsWith(GameNote, StringComparison.Ordinal))
        {
            lines.RemoveAt(noteAt);
        }

        while (noteAt < lines.Count && lines[noteAt].TrimStart().StartsWith(GameNote, StringComparison.Ordinal))
        {
            lines.RemoveAt(noteAt);
            while (noteAt < lines.Count && lines[noteAt].TrimStart().StartsWith("//   ", StringComparison.Ordinal))
            {
                lines.RemoveAt(noteAt);
            }
        }

        if (game.Count > 0)
        {
            string indent = Indent(lines[lastUsing]);
            var note = new List<string> { "" };
            note.AddRange(Wrap(indent, GameNote + " (global namespace, Assembly-CSharp): ", game));
            lines.InsertRange(lastUsing + 1, note);
        }

        // Usings bottom-up so earlier line numbers stay valid.
        foreach (UsingDirectiveSyntax u in usings.OrderByDescending(u => u.SpanStart))
        {
            int line = tree.GetLineSpan(u.Span).StartLinePosition.Line;
            string ns = u.Name.ToString();
            string code = CodePart(lines[line]);

            // Drop a comment block written above this using by an earlier run.
            while (line > 0 && IsOwnComment(lines[line - 1], ns, lines, line - 1))
            {
                int start = line - 1;
                while (start > 0 && lines[start].TrimStart().StartsWith("//   ", StringComparison.Ordinal))
                {
                    start--;
                }

                int count = line - start;
                lines.RemoveRange(start, count);
                line -= count;
            }

            if (!provided.TryGetValue(ns, out var names) || names.Count == 0)
            {
                lines.RemoveAt(line);
                dropped++;
                continue;
            }

            string list = string.Join(", ", names);
            string trailing = code + " // " + list;
            if (trailing.Length <= MaxLine)
            {
                lines[line] = trailing;
            }
            else
            {
                lines[line] = code;
                lines.InsertRange(line, Wrap(Indent(code), "// " + ns + ": ", names));
            }
        }

        return string.Join(newline, lines);
    }

    // A comment block of ours directly above `using ns;`: "// ns: …" possibly followed by
    // "//   …" continuation lines, ending on the line above the using.
    private static bool IsOwnComment(string line, string ns, List<string> lines, int index)
    {
        int i = index;
        while (i > 0 && lines[i].TrimStart().StartsWith("//   ", StringComparison.Ordinal))
        {
            i--;
        }

        return lines[i].TrimStart().StartsWith("// " + ns + ": ", StringComparison.Ordinal);
    }

    private static string CodePart(string line)
    {
        int c = line.IndexOf("//", StringComparison.Ordinal);
        return (c >= 0 ? line.Substring(0, c) : line).TrimEnd();
    }

    private static string Indent(string line) => line.Substring(0, line.Length - line.TrimStart().Length);

    private static INamedTypeSymbol TopType(INamedTypeSymbol t)
    {
        while (t.ContainingType != null)
        {
            t = t.ContainingType;
        }

        return t;
    }

    private static INamespaceSymbol TopNamespace(INamedTypeSymbol t) => TopType(t).ContainingNamespace;

    private static List<string> Wrap(string indent, string first, IEnumerable<string> names)
    {
        var result = new List<string>();
        string current = indent + first;
        bool fresh = true;
        foreach (string n in names)
        {
            string piece = (fresh ? "" : ", ") + n;
            if (!fresh && (current + piece + ",").Length > MaxLine)
            {
                result.Add(current + ",");
                current = indent + "//   " + n;
            }
            else
            {
                current += piece;
            }

            fresh = false;
        }

        result.Add(current);
        return result;
    }
}
