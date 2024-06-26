﻿using ICSharpCode.CodeConverter.Util.FromRoslyn;

namespace ICSharpCode.CodeConverter.CSharp;

internal static class ClashingMemberRenamer
{
    /// <summary>
    /// Renames symbols in a VB project so that they don't clash with rules for C# member names, attempting to rename the least public ones first.
    /// See https://github.com/icsharpcode/CodeConverter/issues/420
    /// </summary>
    public static async Task<Project> RenameClashingSymbolsAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);
        var memberRenames = SymbolRenamer.GetNamespacesAndTypesInAssembly(project, compilation)
            .SelectMany(x => GetSymbolsWithNewNames(x, compilation));
        return await SymbolRenamer.PerformRenamesAsync(project, memberRenames.ToList(), cancellationToken);
    }

    private static IEnumerable<(ISymbol Original, string NewName)> GetSymbolsWithNewNames(INamespaceOrTypeSymbol containerSymbol, Compilation compilation)
    {
        if (containerSymbol.IsNamespace) return Enumerable.Empty<(ISymbol Original, string NewName)>();

        var members = containerSymbol.GetMembers()
            .Where(m => m.Locations.Any(loc => loc.SourceTree != null && compilation.ContainsSyntaxTree(loc.SourceTree)))
            .Where(s => ShouldBeRenamed(containerSymbol, s));
        var symbolSet = containerSymbol.Yield().Concat(members).ToArray();
        return SymbolRenamer.GetSymbolsWithNewNames(symbolSet, new HashSet<string>(symbolSet.Select(SymbolRenamer.GetName)), true);
    }

    private static bool ShouldBeRenamed(INamespaceOrTypeSymbol containerSymbol, ISymbol symbol)
    {
        if (containerSymbol is INamedTypeSymbol namedSymbol && namedSymbol.IsEnumType())
            return false;

        return containerSymbol.Name == symbol.Name;
    }
}