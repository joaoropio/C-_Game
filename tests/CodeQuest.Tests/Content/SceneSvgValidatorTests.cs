namespace CodeQuest.Tests.Content;

public class SceneSvgValidatorTests
{
    private const string Open = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\" aria-hidden=\"true\">";

    public static TheoryData<string> Malicious() => new()
    {
        "<svg/onload=\"alert(1)\"></svg>",
        $"{Open}<rect ONCLICK=\"x()\"/></svg>",
        $"{Open}<rect onmouseover = \"x()\"/></svg>",
        $"{Open}<script>alert(1)</script></svg>",
        $"{Open}<foreignObject><div/></foreignObject></svg>",
        $"{Open}<image href=\"https://example.com/a.png\"/></svg>",
        $"{Open}<a href=\"javascript:alert(1)\"><rect/></a></svg>",
        $"{Open}<use href=\"https://example.com/a.svg#x\"/></svg>",
        $"{Open}<use xlink:href=\"data:image/svg+xml,x\" xmlns:xlink=\"http://www.w3.org/1999/xlink\"/></svg>",
        $"{Open}<animate attributeName=\"x\" to=\"1\"/></svg>",
        $"{Open}<set attributeName=\"x\" to=\"1\"/></svg>",
        $"{Open}<style>rect{{fill:url(http://x.test/a)}}</style></svg>",
        $"{Open}<rect fill=\"url(https://x.test/a)\"/></svg>",
        $"{Open}<iframe src=\"https://x.test\"/></svg>",
        $"{Open}<img/src=x onerror=\"alert(1)\"/></svg>",
        "<div></div>",
        $"{Open}<!--><img src=x onerror=alert(1)>--></svg>",
        $"{Open}<!---><img src=x onerror=alert(1)>--></svg>",
        $"{Open}<?x ><img src=x onerror=alert(1)>?></svg>",
        $"{Open}<rect fill=\"u\\rl(https://x.test/a)\"/></svg>",
        $"{Open}<rect style=\"background-image:image-set('https://x.test/a' 1x)\"/></svg>",
        $"{Open}<rect style=\"width:expression(alert(1))\"/></svg>",
        $"{Open}<rect fill=\"image(https://x.test/a)\"/></svg>",
        "<!DOCTYPE svg [<!ENTITY e \"x\">]><svg xmlns=\"http://www.w3.org/2000/svg\"><rect/></svg>",
        $"{Open}<rect></svg>",
    };

    [Theory]
    [MemberData(nameof(Malicious))]
    public void Validator_RejectsMaliciousOrMalformedSvg(string svg) =>
        Assert.True(SceneSvgValidator.Problems(svg).Count > 0, $"accepted: {svg}");

    [Fact]
    public void Validator_AcceptsLocalReferencesAndPlainShapes() =>
        Assert.Empty(SceneSvgValidator.Problems(
            $"{Open}<defs><g id=\"p\"><circle r=\"1\"/></g></defs><use href=\"#p\" color=\"#fff\"/><rect fill=\"url(#p)\"/></svg>"));
}
