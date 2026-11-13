using System.Xml.Linq;

namespace CodeQuest.Tests.Content;

/// <summary>Allowlist check for scene.svg files, which are injected as markup: anything outside the list is a problem.</summary>
public static class SceneSvgValidator
{
    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "svg", "g", "defs", "use", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "linearGradient", "radialGradient", "stop",
    };

    public static IReadOnlyList<string> Problems(string svg)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(svg);
        }
        catch (System.Xml.XmlException exception)
        {
            return [$"not well-formed XML: {exception.Message}"];
        }

        var problems = new List<string>();
        if (document.Root?.Name.LocalName != "svg")
        {
            problems.Add("root element is not svg");
        }

        foreach (var element in document.Descendants())
        {
            if (!AllowedElements.Contains(element.Name.LocalName) || element.Name.NamespaceName is not ("" or "http://www.w3.org/2000/svg"))
            {
                problems.Add($"element not allowed: {element.Name}");
            }

            foreach (var attribute in element.Attributes())
            {
                CheckAttribute(attribute, problems);
            }
        }

        return problems;
    }

    private static void CheckAttribute(XAttribute attribute, List<string> problems)
    {
        var name = attribute.Name.LocalName;
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"event attribute: {name}");
        }

        if (name.Equals("href", StringComparison.OrdinalIgnoreCase) && !attribute.Value.StartsWith('#'))
        {
            problems.Add($"external href: {attribute.Value}");
        }

        var rest = attribute.Value;
        for (var index = rest.IndexOf("url(", StringComparison.OrdinalIgnoreCase); index >= 0;
             index = rest.IndexOf("url(", index + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (!rest.AsSpan(index + 4).TrimStart().StartsWith("#"))
            {
                problems.Add($"non-local url() in {name}");
            }
        }
    }
}
