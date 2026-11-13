using System.Xml;

namespace CodeQuest.Tests.Content;

/// <summary>
/// Allowlist check for scene.svg files, which are injected as HTML markup: the HTML parser reads comments,
/// processing instructions and unknown attributes differently from XML, so only plain elements with known
/// attributes are accepted.
/// </summary>
public static class SceneSvgValidator
{
    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "svg", "g", "defs", "use", "path", "rect", "circle", "ellipse", "line", "polyline", "polygon",
        "linearGradient", "radialGradient", "stop",
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.Ordinal)
    {
        "xmlns", "xmlns:xlink", "viewBox", "preserveAspectRatio", "width", "height", "x", "y", "x1", "y1", "x2", "y2",
        "cx", "cy", "r", "rx", "ry", "d", "points", "fill", "fill-opacity", "stroke", "stroke-width", "stroke-opacity",
        "stroke-linecap", "stroke-linejoin", "stroke-dasharray", "opacity", "transform", "color", "id", "href",
        "xlink:href", "offset", "stop-color", "stop-opacity", "gradientUnits", "aria-hidden", "focusable",
    };

    private static readonly string[] ForbiddenInValues = ["\\", "image-set(", "image(", "expression("];

    public static IReadOnlyList<string> Problems(string svg)
    {
        var problems = new List<string>();
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreComments = false, IgnoreProcessingInstructions = false };
        try
        {
            using var reader = XmlReader.Create(new StringReader(svg), settings);
            var seenRoot = false;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        CheckElement(reader, !seenRoot, problems);
                        seenRoot = true;
                        break;
                    case XmlNodeType.EndElement:
                    case XmlNodeType.Whitespace:
                    case XmlNodeType.SignificantWhitespace:
                        break;
                    default:
                        problems.Add($"node not allowed: {reader.NodeType}");
                        break;
                }
            }
        }
        catch (XmlException exception)
        {
            problems.Add($"not well-formed XML: {exception.Message}");
        }

        return problems;
    }

    private static void CheckElement(XmlReader reader, bool isRoot, List<string> problems)
    {
        if (isRoot && reader.LocalName != "svg")
        {
            problems.Add("root element is not svg");
        }

        if (!AllowedElements.Contains(reader.Name) || reader.NamespaceURI != "http://www.w3.org/2000/svg" && reader.NamespaceURI != "")
        {
            problems.Add($"element not allowed: {reader.Name}");
        }

        while (reader.MoveToNextAttribute())
        {
            CheckAttribute(reader.Name, reader.Value, problems);
        }

        reader.MoveToElement();
    }

    private static void CheckAttribute(string name, string value, List<string> problems)
    {
        if (!AllowedAttributes.Contains(name))
        {
            problems.Add($"attribute not allowed: {name}");
            return;
        }

        if (name is "href" or "xlink:href" && !value.StartsWith('#'))
        {
            problems.Add($"external href: {value}");
        }

        if (ForbiddenInValues.Any(forbidden => value.Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add($"forbidden content in {name}");
        }

        for (var index = value.IndexOf("url(", StringComparison.OrdinalIgnoreCase); index >= 0;
             index = value.IndexOf("url(", index + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (!value.AsSpan(index + 4).TrimStart().StartsWith("#"))
            {
                problems.Add($"non-local url() in {name}");
            }
        }
    }
}
