using Shouldly;

namespace Carotte.Tests;

public class MessageTypeResolverTests
{
    private readonly MessageTypeResolver _resolver = new();

    [Fact]
    public void GetTypeIdentifier_ShouldReturnClassName_ByDefault()
    {
        var identifier = _resolver.GetTypeIdentifier(typeof(StandardMessage));
        identifier.ShouldBe(nameof(StandardMessage));
    }

    [Fact]
    public void GetTypeIdentifier_ShouldReturnCustomName_WhenMessageTypeAttributeIsPresent()
    {
        var identifier = _resolver.GetTypeIdentifier(typeof(CustomAliasedMessage));
        identifier.ShouldBe("custom.v1.message");
    }

    [Fact]
    public void ResolveType_ShouldResolveByShortName()
    {
        var result = _resolver.ResolveType("StandardMessage", [typeof(StandardMessage), typeof(OtherMessage)]);
        result.ShouldBe(typeof(StandardMessage));
    }

    [Fact]
    public void ResolveType_ShouldResolveByFullName()
    {
        var result = _resolver.ResolveType(typeof(StandardMessage).FullName, [typeof(StandardMessage), typeof(OtherMessage)]);
        result.ShouldBe(typeof(StandardMessage));
    }

    [Fact]
    public void ResolveType_ShouldResolveByCustomMessageTypeAttribute()
    {
        var result = _resolver.ResolveType("custom.v1.message", [typeof(StandardMessage), typeof(CustomAliasedMessage)]);
        result.ShouldBe(typeof(CustomAliasedMessage));
    }

    [Fact]
    public void ResolveType_ShouldResolveByAssemblyQualifiedName()
    {
        var aqn = "Carotte.Tests.MessageTypeResolverTests+StandardMessage, Carotte.Tests";
        var result = _resolver.ResolveType(aqn, [typeof(StandardMessage), typeof(OtherMessage)]);
        result.ShouldBe(typeof(StandardMessage));
    }

    [Fact]
    public void ResolveType_ShouldResolveByUrn()
    {
        var urn = "urn:message:Carotte.Tests:StandardMessage";
        var result = _resolver.ResolveType(urn, [typeof(StandardMessage), typeof(OtherMessage)]);
        result.ShouldBe(typeof(StandardMessage));
    }

    [Fact]
    public void ResolveType_ShouldBeCaseInsensitive()
    {
        var result = _resolver.ResolveType("standardmessage", [typeof(StandardMessage)]);
        result.ShouldBe(typeof(StandardMessage));
    }

    [Fact]
    public void ResolveType_ShouldReturnNull_WhenTypeIsExplicitlySpecifiedButUnknown_ForSingleMessageConsumer()
    {
        var result = _resolver.ResolveType("UnknownType", [typeof(StandardMessage)]);
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveType_ShouldInferType_WhenTypeIsNullOrEmpty_ForSingleMessageConsumer()
    {
        var resultNull = _resolver.ResolveType(null, [typeof(StandardMessage)]);
        resultNull.ShouldBe(typeof(StandardMessage));

        var resultEmpty = _resolver.ResolveType(string.Empty, [typeof(StandardMessage)]);
        resultEmpty.ShouldBe(typeof(StandardMessage));

        var resultWhitespace = _resolver.ResolveType("   ", [typeof(StandardMessage)]);
        resultWhitespace.ShouldBe(typeof(StandardMessage));
    }

    [Fact]
    public void ResolveType_ShouldReturnNull_WhenTypeIsNullOrEmpty_ForMultiMessageConsumer()
    {
        var resultNull = _resolver.ResolveType(null, [typeof(StandardMessage), typeof(OtherMessage)]);
        resultNull.ShouldBeNull();

        var resultEmpty = _resolver.ResolveType(string.Empty, [typeof(StandardMessage), typeof(OtherMessage)]);
        resultEmpty.ShouldBeNull();
    }

    [Fact]
    public void ResolveType_ShouldReturnNull_WhenCandidatesAreEmpty()
    {
        var result = _resolver.ResolveType("StandardMessage", []);
        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveType_ShouldReturnNull_WhenCandidatesAreNull()
    {
        var result = _resolver.ResolveType("StandardMessage", null!);
        result.ShouldBeNull();
    }

    [Fact]
    public void GetTypeIdentifier_ShouldThrowArgumentNullException_WhenMessageTypeIsNull()
    {
        Should.Throw<ArgumentNullException>(() => _resolver.GetTypeIdentifier(null!));
    }

    private class StandardMessage;

    private class OtherMessage;

    [MessageType("custom.v1.message")]
    private class CustomAliasedMessage;
}
