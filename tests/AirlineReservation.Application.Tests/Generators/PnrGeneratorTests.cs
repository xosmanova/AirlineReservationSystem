using AirlineReservation.Application.Common.Generators;
using AirlineReservation.Application.Common.Interfaces;
using AirlineReservation.Application.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AirlineReservation.Application.Tests.Generators;

public class PnrGeneratorTests
{
    private readonly IPnrGenerator _sut = new PnrGenerator();

    [Fact]
    public void Generate_ShouldReturnExactSixCharactersLength()
    {
        // Act
        var pnr = _sut.Generate();

        // Assert
        Assert.NotNull(pnr);
        Assert.Equal(6, pnr.Length);
    }

    [Fact]
    public void Generate_ShouldContainOnlyAllowedAlphabetCharacters()
    {
        // Arrange
        const string allowedChars = PnrGenerator.AllowedCharacters;

        // Act
        var pnr = _sut.Generate();

        // Assert
        Assert.All(pnr, ch => Assert.Contains(ch, allowedChars));
    }

    [Fact]
    public void Generate_ShouldNeverContainForbiddenCharacters_ZeroOOneI()
    {
        // Arrange
        char[] forbidden = ['0', 'O', '1', 'I'];

        // Act & Assert across 1,000 quick iterations
        for (int i = 0; i < 1_000; i++)
        {
            var pnr = _sut.Generate();
            foreach (var ch in forbidden)
            {
                Assert.DoesNotContain(ch, pnr);
            }
        }
    }

    [Fact]
    public void Generate_OneHundredThousandSamples_ShouldDemonstrateHighEntropyAndZeroInvalidCharacters()
    {
        // Arrange
        const int sampleSize = 100_000;
        var hashSet = new HashSet<string>(sampleSize);
        var allowedSet = new HashSet<char>(PnrGenerator.AllowedCharacters);
        char[] forbidden = ['0', 'O', '1', 'I'];

        // Act
        for (int i = 0; i < sampleSize; i++)
        {
            var pnr = _sut.Generate();

            // Length check
            Assert.Equal(6, pnr.Length);

            // Valid chars
            for (int c = 0; c < pnr.Length; c++)
            {
                Assert.Contains(pnr[c], allowedSet);
            }

            // Forbidden chars
            for (int f = 0; f < forbidden.Length; f++)
            {
                Assert.DoesNotContain(forbidden[f], pnr);
            }

            hashSet.Add(pnr);
        }

        // Assert: In 32^6 space (1,073,741,824), 100,000 samples statistically have ~4-5 collisions.
        // Uniqueness must easily exceed 99.9% (>= 99,900 unique entries).
        Assert.True(hashSet.Count >= 99_900,
            $"Expected at least 99,900 unique PNRs out of 100,000, but got {hashSet.Count}");
    }

    [Fact]
    public void PnrGenerator_ShouldBeRegisteredAsSingletonInServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddApplicationServices();

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPnrGenerator));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(PnrGenerator), descriptor.ImplementationType);
    }
}
