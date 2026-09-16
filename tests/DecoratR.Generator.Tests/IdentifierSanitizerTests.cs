using AwesomeAssertions;
using Xunit;

namespace DecoratR.Generator.Tests;

public class IdentifierSanitizerTests
{
    [Theory]
    [InlineData("My.App", "My.App")]
    [InlineData("My-App.Web", "My_App.Web")]
    [InlineData("1Foo", "_1Foo")]
    [InlineData("class.Lib", "@class.Lib")]
    [InlineData("A B", "A_B")]
    [InlineData("My..App", "My._.App")]
    [InlineData("Ünïcödé.Name", "Ünïcödé.Name")]
    public void ToNamespace_ProducesValidNamespace(string input, string expected) =>
        IdentifierSanitizer.ToNamespace(input).Should().Be(expected);

    [Theory]
    [InlineData("global::App.Logging.LoggingDecorator", "App_Logging_LoggingDecorator")]
    [InlineData("global::LoggingDecorator", "LoggingDecorator")]
    [InlineData("global::@class.Dec", "_class_Dec")]
    public void FromTypeName_ProducesIdentifierFragment(string input, string expected) =>
        IdentifierSanitizer.FromTypeName(input).Should().Be(expected);

    [Fact]
    public void ToIdentifier_EmptyInput_YieldsUnderscore() =>
        IdentifierSanitizer.ToIdentifier(string.Empty).Should().Be("_");
}
