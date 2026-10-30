using System.Net;
using System.Text.RegularExpressions;

namespace Claudette.App.Tests;

/// <summary>
/// A style that looks for a class on an ancestor names the ancestor's type: Settings → Appearance's classes are on
/// <c>Controls/AppearanceScope</c> (<c>controls|AppearanceScope.claude Border.composer</c>; DESIGN.md §3, "Visual style").
/// One that starts with the bare class (<c>.claude Border.composer</c>) could match any ancestor, so Avalonia watches the
/// classes of every ancestor of every control the style might apply to: 1.5 million watchers for six tabs.
/// </summary>
public partial class AppearanceScopeTests
{
    [Fact]
    public void No_style_looks_for_a_class_on_every_ancestor()
    {
        var app = Path.Combine(RepoRoot(), "src", "Claudette.App");
        var found = Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(app, file).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .SelectMany(file => SelectorAttribute().Matches(File.ReadAllText(file))
                .SelectMany(m => Alternatives(WebUtility.HtmlDecode(m.Groups[1].Value)))
                .Where(LooksAtEveryAncestor)
                .Select(selector => $"{Path.GetRelativePath(app, file)}: {selector}"))
            .ToList();

        Assert.Empty(found);
    }

    [Theory]
    [InlineData(".claude Border.composer", true)]
    [InlineData(".claude.compact controls|Card", true)]
    [InlineData(":not(.claude).quietsteps controls|Card", true)]
    [InlineData("controls|AppearanceScope.claude Border.composer", false)]
    [InlineData(":is(Window).reducemotion Border.chip", false)]
    [InlineData("^:pointerover /template/ Border#PART_Card", false)]
    [InlineData(".compact > ContentPresenter", false)]
    [InlineData(".copied /template/ TextBlock#CopyLabel", false)]
    [InlineData("#Composer TextBlock", false)]
    [InlineData(".claude", false)]
    public void A_selector_looks_at_every_ancestor_when_it_starts_without_a_type_and_then_looks_inside(string selector, bool every) =>
        Assert.Equal(every, LooksAtEveryAncestor(selector));

    /// <summary>
    /// Its first step has no type, only a class, a pseudo-class or a property (a name can't change, so it's never watched),
    /// and a descendant combinator follows it. A child (<c>&gt;</c>) or <c>/template/</c> looks at one element, and
    /// <c>^</c> is its parent style's selector.
    /// </summary>
    private static bool LooksAtEveryAncestor(string selector)
    {
        if (selector.Length == 0 || selector[0] is not ('.' or ':' or '[') || selector.StartsWith(":is(", StringComparison.Ordinal))
        {
            return false;
        }
        var end = FirstStepEnd(selector);
        if (end == selector.Length || !char.IsWhiteSpace(selector[end]))
        {
            return false;
        }
        var rest = selector[end..].TrimStart();
        return !rest.StartsWith('>') && !rest.StartsWith("/template/", StringComparison.Ordinal);
    }

    private static int FirstStepEnd(string selector)
    {
        var depth = 0;
        for (var i = 0; i < selector.Length; i++)
        {
            switch (selector[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case var c when depth == 0 && (char.IsWhiteSpace(c) || c is '>' or '/'):
                    return i;
            }
        }
        return selector.Length;
    }

    /// <summary>A selector's alternatives: <c>a, b</c> is two.</summary>
    private static IEnumerable<string> Alternatives(string selector)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < selector.Length; i++)
        {
            switch (selector[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return selector[start..i].Trim();
                    start = i + 1;
                    break;
            }
        }
        yield return selector[start..].Trim();
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Claudette.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Couldn't find the repository root.");
    }

    [GeneratedRegex("Selector=\"([^\"]*)\"")]
    private static partial Regex SelectorAttribute();
}
