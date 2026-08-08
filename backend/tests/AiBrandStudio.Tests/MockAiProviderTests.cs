using AiBrandStudio.Infrastructure.AI;
using FluentAssertions;

namespace AiBrandStudio.Tests;

public class MockAiProviderTests
{
    [Fact]
    public async Task MockTextProvider_ReturnsLabeledBrandFields()
    {
        var provider = new MockTextAiProvider();
        var result = await provider.GenerateTextAsync("system", """{"BusinessName":"MVC Core","Industry":"SaaS"}""");
        result.Should().Contain("NAME:");
        result.Should().Contain("TAGLINE:");
        result.Should().Contain("MVC Core");
    }

    [Fact]
    public async Task MockImageProvider_ReturnsSvg()
    {
        var provider = new MockImageAiProvider();
        var (bytes, contentType, svg) = await provider.GenerateImageAsync("logo");
        contentType.Should().Be("image/svg+xml");
        svg.Should().Contain("<svg");
        bytes.Should().NotBeEmpty();
    }
}
