using Transgrid.Functions.Services;

namespace Transgrid.Functions.Tests;

/// <summary>
/// Unit tests for ReferenceDataService.
/// </summary>
public class ReferenceDataServiceTests
{
    private readonly ReferenceDataService _sut;

    public ReferenceDataServiceTests()
    {
        _sut = new ReferenceDataService();
    }

    [Fact]
    public async Task GetReferenceDataAsync_ReturnsNonNullData()
    {
        // Act
        var result = await _sut.GetReferenceDataAsync();

        // Assert
        result.Should().NotBeNull();
        result.Locations.Should().NotBeEmpty();
        result.Vehicles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetReferenceDataAsync_ContainsEurostarLocations()
    {
        // Act
        var result = await _sut.GetReferenceDataAsync();

        // Assert
        result.Locations.Should().ContainKey("GBSTP"); // London St Pancras
        result.Locations.Should().ContainKey("FRPNO"); // Paris Nord
        result.Locations.Should().ContainKey("BEBMI"); // Brussels Midi
    }

    [Theory]
    [InlineData("GBSTP", "7015400")]
    [InlineData("FRPNO", "8727100")]
    [InlineData("FRLIL", "8722326")]
    [InlineData("BEBMI", "8814001")]
    public void GetUicCode_KnownLocation_ReturnsCorrectCode(string locationCode, string expectedUic)
    {
        // Act
        var result = _sut.GetUicCode(locationCode);

        // Assert
        result.Should().Be(expectedUic);
    }

    [Fact]
    public void GetUicCode_UnknownLocation_ReturnsDefaultFormat()
    {
        // Act
        var result = _sut.GetUicCode("UNKNOWN");

        // Assert
        result.Should().Be("0000UNKNOWN");
    }

    [Fact]
    public void GetLocation_KnownLocation_ReturnsLocationReference()
    {
        // Act
        var result = _sut.GetLocation("GBSTP");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("London St Pancras");
        result.Country.Should().Be("GB");
        result.Type.Should().Be("STATION");
    }

    [Fact]
    public void GetLocation_UnknownLocation_ReturnsNull()
    {
        // Act
        var result = _sut.GetLocation("UNKNOWN");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetReferenceDataAsync_ContainsEurostarVehicles()
    {
        // Act
        var result = await _sut.GetReferenceDataAsync();

        // Assert
        result.Vehicles.Should().ContainKey("E320"); // Eurostar e320
        result.Vehicles.Should().ContainKey("E300"); // Eurostar e300
    }

    [Fact]
    public async Task GetReferenceDataAsync_VehiclesHaveCorrectProperties()
    {
        // Act
        var result = await _sut.GetReferenceDataAsync();
        var e320 = result.Vehicles["E320"];

        // Assert
        e320.VehicleType.Should().Be("EMU");
        e320.MaxPassengers.Should().Be(900);
        e320.TrainClass.Should().Be("EUROSTAR");
    }

    [Fact]
    public async Task GetReferenceDataAsync_LocationsHaveRequiredFields()
    {
        // Act
        var result = await _sut.GetReferenceDataAsync();

        // Assert - All locations should have required fields
        foreach (var location in result.Locations.Values)
        {
            location.Code.Should().NotBeNullOrWhiteSpace();
            location.Name.Should().NotBeNullOrWhiteSpace();
            location.Country.Should().NotBeNullOrWhiteSpace();
            location.UicCode.Should().NotBeNullOrWhiteSpace();
            location.Type.Should().NotBeNullOrWhiteSpace();
        }
    }
}
