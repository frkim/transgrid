using Transgrid.Functions.Models;
using Transgrid.Functions.Services;

namespace Transgrid.Functions.Tests;

/// <summary>
/// Unit tests for XmlValidationService.
/// </summary>
public class XmlValidationServiceTests
{
    private readonly XmlValidationService _sut;
    private readonly XmlTransformService _transformService;
    private readonly ReferenceData _referenceData;

    public XmlValidationServiceTests()
    {
        _sut = new XmlValidationService();
        _transformService = new XmlTransformService();
        var referenceDataService = new ReferenceDataService();
        _referenceData = referenceDataService.GetReferenceDataAsync().Result;
    }

    [Fact]
    public void Validate_ValidXml_ReturnsIsValidTrue()
    {
        // Arrange
        var trainPlan = CreateValidTrainPlan();
        var xml = _transformService.Transform(trainPlan, _referenceData);

        // Act
        var result = _sut.Validate(xml);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MalformedXml_ReturnsIsValidFalse()
    {
        // Arrange
        var malformedXml = "<?xml version=\"1.0\"?><root><unclosed>";

        // Act
        var result = _sut.Validate(malformedXml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void Validate_EmptyTrainNumber_ReturnsValidationError()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <PassengerTrainCompositionProcessMessage xmlns=""http://taf-jsg.info/schemas/2.1.6/ptcpm"">
                <MessageHeader>
                    <MessageIdentifier>TEST-123</MessageIdentifier>
                    <MessageType>PTCPMRequest</MessageType>
                </MessageHeader>
                <TrainInformation>
                    <TrainNumber></TrainNumber>
                    <TravelDate>2026-01-20</TravelDate>
                </TrainInformation>
            </PassengerTrainCompositionProcessMessage>";

        // Act
        var result = _sut.Validate(xml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("TrainNumber"));
    }

    [Fact]
    public void Validate_MissingTravelDate_ReturnsValidationError()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <PassengerTrainCompositionProcessMessage xmlns=""http://taf-jsg.info/schemas/2.1.6/ptcpm"">
                <MessageHeader>
                    <MessageIdentifier>TEST-123</MessageIdentifier>
                    <MessageType>PTCPMRequest</MessageType>
                </MessageHeader>
                <TrainInformation>
                    <TrainNumber>9001</TrainNumber>
                </TrainInformation>
            </PassengerTrainCompositionProcessMessage>";

        // Act
        var result = _sut.Validate(xml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("TravelDate"));
    }

    [Fact]
    public void Validate_InvalidDateFormat_ReturnsValidationError()
    {
        // Arrange
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
            <PassengerTrainCompositionProcessMessage xmlns=""http://taf-jsg.info/schemas/2.1.6/ptcpm"">
                <MessageHeader>
                    <MessageIdentifier>TEST-123</MessageIdentifier>
                    <MessageType>PTCPMRequest</MessageType>
                </MessageHeader>
                <TrainInformation>
                    <TrainNumber>9001</TrainNumber>
                    <TravelDate>20-01-2026</TravelDate>
                </TrainInformation>
            </PassengerTrainCompositionProcessMessage>";

        // Act
        var result = _sut.Validate(xml);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Message.Contains("date format") || e.Message.Contains("TravelDate"));
    }

    [Fact]
    public void Validate_GeneratedXml_PassesValidation()
    {
        // Arrange
        var trainPlan = new TrainPlanInput
        {
            Id = "test-456",
            ServiceCode = "9002",
            Pathway = "HS1",
            TravelDate = "2026-02-15",
            Origin = "FRPNO",
            Destination = "GBSTP",
            Status = "ACTIVE",
            PlanType = "STANDARD",
            Country = "FR",
            PassagePoints = new List<PassagePoint>
            {
                new() { LocationCode = "FRPNO", DepartureTime = "07:43" },
                new() { LocationCode = "FRLIL", ArrivalTime = "08:43", DepartureTime = "08:46" },
                new() { LocationCode = "GBSTP", ArrivalTime = "09:39" }
            }
        };
        var xml = _transformService.Transform(trainPlan, _referenceData);

        // Act
        var result = _sut.Validate(xml);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("9001", true)]
    [InlineData("9001-A", true)]
    [InlineData("EURO_9001", true)]
    [InlineData("", false)]
    [InlineData("AB", true)]
    [InlineData("ThisIsAVeryLongTrainNumberThatExceedsTwentyChars", false)]
    public void ValidateTrainNumber_VariousFormats_ReturnsExpectedResult(string trainNumber, bool shouldBeValid)
    {
        // Arrange
        var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
            <PassengerTrainCompositionProcessMessage xmlns=""http://taf-jsg.info/schemas/2.1.6/ptcpm"">
                <MessageHeader>
                    <MessageIdentifier>TEST-123</MessageIdentifier>
                    <MessageType>PTCPMRequest</MessageType>
                </MessageHeader>
                <TrainInformation>
                    <TrainNumber>{trainNumber}</TrainNumber>
                    <TravelDate>2026-01-20</TravelDate>
                </TrainInformation>
            </PassengerTrainCompositionProcessMessage>";

        // Act
        var result = _sut.Validate(xml);

        // Assert
        if (shouldBeValid)
        {
            result.Errors.Should().NotContain(e => e.Message.Contains("train number"));
        }
        else
        {
            result.Errors.Should().Contain(e => 
                e.Message.Contains("train number", StringComparison.OrdinalIgnoreCase) || 
                e.Message.Contains("TrainNumber"));
        }
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
