using FluentAssertions;
using miSWIYUverifier.Models;
using Xunit;

namespace miSWIYUverifier.Core.Tests.Models;

public class IdentityDataTests
{
    [Fact]
    public void IsComplete_True_When_Name_And_BirthDate_Present()
    {
        var identity = new IdentityData
        {
            GivenName  = "Maria",
            FamilyName = "Muster",
            BirthDate  = "1990-05-17",
        };

        identity.IsComplete.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, "Muster", "1990-05-17")]
    [InlineData("Maria", null, "1990-05-17")]
    [InlineData("Maria", "Muster", null)]
    [InlineData("", "Muster", "1990-05-17")]
    [InlineData("   ", "Muster", "1990-05-17")]
    public void IsComplete_False_When_Any_Core_Field_Missing(
        string? given, string? family, string? birth)
    {
        var identity = new IdentityData
        {
            GivenName  = given,
            FamilyName = family,
            BirthDate  = birth,
        };

        identity.IsComplete.Should().BeFalse();
    }
}
