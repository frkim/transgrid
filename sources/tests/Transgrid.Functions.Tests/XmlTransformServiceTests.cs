using Transgrid.Functions.Models;
using Transgrid.Functions.Services;

namespace Transgrid.Functions.Tests;

/// <summary>
/// Unit tests for XmlTransformService - the core transformation logic.
/// </summary>
public class XmlTransformServiceTests
{
    private readonly XmlTransformService _sut;
    private readonly ReferenceData _referenceData;

    public XmlTransformServiceTests()
    {
        _sut = new XmlTransformService();
        var referenceDataService = new ReferenceDataService();
        _referenceData = referenceDataService.GetReferenceDataAsync().Result;
    }

    [Fact]
    public void Transform_ValidTrainPlan_ReturnsValidXml()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();

        // Act
        var xml = _sut.Transform(trainPlan, _referenceData);

        // Assert
        xml.Should().NotBeNullOrWhiteSpace();
        xml.Should().StartWith("<?xml version=\"1.0\"");
        xml.Should().Contain("PassengerTrainCompositionProcessMessage");
    }

    [Fact]
    public void Transform_ValidTrainPlan_ContainsCorrectNamespace()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();

        // Act
        var xml = _sut.Transform(trainPlan, _referenceData);

        // Assert
        xml.Should().Contain("http://taf-jsg.info/schemas/2.1.6/ptcpm");
    }

    [Fact]
    public void Transform_ValidTrainPlan_ContainsMessageHeader()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();

        // Act
        var xml = _sut.Transform(trainPlan, _referenceData);

        // Assert
        xml.Should().Contain("<MessageHeader>");
        xml.Should().Contain("<MessageIdentifier>");
        xml.Should().Contain("<MessageType>PTCPMRequest</MessageType>");
        xml.Should().Contain("<SenderReference>EUROSTAR</SenderReference>");
        xml.Should().Contain("<RecipientReference>RNE</RecipientReference>");
    }

    [Fact]
    public void Transform_ValidTrainPlan_ContainsTrainInformation()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();

        // Act
        var xml = _sut.Transform(trainPlan, _referenceData);

        // Assert
        xml.Should().Contain("<TrainInformation>");
        xml.Should().Contain($"<TrainNumber>{trainPlan.ServiceCode}</TrainNumber>");
        xml.Should().Contain($"<TravelDate>{trainPlan.TravelDate}</TravelDate>");
        xml.Should().Contain($"<OperationalPathway>{trainPlan.Pathway}</OperationalPathway>");
    }

    [Fact]
    public void Transform_TrainPlanWithPassagePoints_IncludesAllPoints()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();
        trainPlan = trainPlan with
        {
            PassagePoints = new List<PassagePoint>
            {
                new() { LocationCode = "GBSTP", DepartureTime = "06:00" },
                new() { LocationCode = "GBEBF", ArrivalTime = "06:17", DepartureTime = "06:19" },
                new() { LocationCode = "FRPNO", ArrivalTime = "09:17" }
            }
        };

        // Act
        var xml = _sut.Transform(trainPlan, _referenceData);

        // Assert
        xml.Should().Contain("<PassagePoints>");
        xml.Should().Contain("<LocationCode>GBSTP</LocationCode>");
        xml.Should().Contain("<LocationCode>GBEBF</LocationCode>");
        xml.Should().Contain("<LocationCode>FRPNO</LocationCode>");
    }

    [Fact]
    public void Transform_LocationWithReferenceData_IncludesUicCode()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();

        // Act
        var xml = _sut.Transform(trainPlan, _referenceData);

        // Assert
        // GBSTP UIC code should be included
        xml.Should().Contain("<LocationUICCode>7015400</LocationUICCode>");
        // FRPNO UIC code should be included
        xml.Should().Contain("<LocationUICCode>8727100</LocationUICCode>");
    }

    [Fact]
    public void Transform_ValidTrainPlan_ContainsTrainComposition()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();

        // Act
        var xml = _sut.Transform(trainPlan, _referenceData);

        // Assert
        xml.Should().Contain("<TrainComposition>");
        xml.Should().Contain("<TrainUnitType>E320</TrainUnitType>");
        xml.Should().Contain("<PassengerCapacity>");
    }

    [Fact]
    public void Transform_NullTrainPlan_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _sut.Transform(null!, _referenceData);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("trainPlan");
    }

    [Fact]
    public void Transform_NullReferenceData_ThrowsArgumentNullException()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();

        // Act & Assert
        var act = () => _sut.Transform(trainPlan, null!);
        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("referenceData");
    }

    [Fact]
    public void Transform_EmptyServiceCode_ThrowsArgumentException()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan() with { ServiceCode = "" };

        // Act & Assert
        var act = () => _sut.Transform(trainPlan, _referenceData);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*ServiceCode*");
    }

    [Fact]
    public void Transform_ValidXml_IsWellFormed()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();

        // Act
        var xml = _sut.Transform(trainPlan, _referenceData);

        // Assert - Should not throw when parsing
        var doc = new System.Xml.XmlDocument();
        var loadAction = () => doc.LoadXml(xml);
        loadAction.Should().NotThrow();
    }

    [Theory]
    [InlineData("9001", "2026-01-20", "GBSTP", "FRPNO")]
    [InlineData("9002", "2026-02-15", "FRPNO", "GBSTP")]
    [InlineData("9123", "2026-03-10", "GBSTP", "BEBMI")]
    public void Transform_VariousTrainPlans_ProducesValidXml(
        string serviceCode, string travelDate, string origin, string destination)
    {
        // Arrange
        var trainPlan = new TrainPlanInput
        {
            Id = Guid.NewGuid().ToString(),
            ServiceCode = serviceCode,
            TravelDate = travelDate,
            Origin = origin,
            Destination = destination,
            Pathway = "HS1",
            Status = "ACTIVE",
            PlanType = "STANDARD",
            Country = "GB",
            PassagePoints = new List<PassagePoint>
            {
                new() { LocationCode = origin },
                new() { LocationCode = destination }
            }
        };

        // Act
        var xml = _sut.Transform(trainPlan, _referenceData);

        // Assert
        xml.Should().Contain($"<TrainNumber>{serviceCode}</TrainNumber>");
        xml.Should().Contain($"<TravelDate>{travelDate}</TravelDate>");
    }

    private static TrainPlanInput CreateValidTrainPlan()
    {
        return new TrainPlanInput
        {
            Id = "test-123",
            ServiceCode = "9001",
            Pathway = "HS1",
            TravelDate = "2026-01-20",
            Origin = "GBSTP",
            Destination = "FRPNO",
            Status = "ACTIVE",
            PlanType = "STANDARD",
            Country = "GB",
            PassagePoints = new List<PassagePoint>
            {
                new() { LocationCode = "GBSTP", DepartureTime = "06:00" },
                new() { LocationCode = "FRPNO", ArrivalTime = "09:17" }
            }
        };
    }
}
