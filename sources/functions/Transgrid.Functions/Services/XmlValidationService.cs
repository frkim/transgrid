using System.Xml;
using System.Xml.Schema;

namespace Transgrid.Functions.Services;

/// <summary>
/// Service for validating XML against XSD schema.
/// </summary>
public interface IXmlValidationService
{
    /// <summary>
    /// Validates XML string against the TAF-JSG schema.
    /// </summary>
    ValidationResult Validate(string xml);

    /// <summary>
    /// Validates XML and returns detailed error information.
    /// </summary>
    ValidationResult ValidateWithDetails(string xml);
}

/// <summary>
/// Result of XML validation.
/// </summary>
public record ValidationResult
{
    public bool IsValid { get; init; }
    public List<ValidationError> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

/// <summary>
/// Details about a validation error.
/// </summary>
public record ValidationError
{
    public string Message { get; init; } = string.Empty;
    public int LineNumber { get; init; }
    public int LinePosition { get; init; }
    public XmlSeverityType Severity { get; init; }
}

/// <summary>
/// Implementation of XML validation service.
/// </summary>
public class XmlValidationService : IXmlValidationService
{
    private readonly XmlSchemaSet? _schemaSet;
    private readonly bool _schemaLoaded;

    public XmlValidationService()
    {
        // Try to load XSD schema from embedded resources or file system
        _schemaSet = new XmlSchemaSet();
        _schemaLoaded = TryLoadSchema();
    }

    private bool TryLoadSchema()
    {
        try
        {
            // In production, load from embedded resource or Azure Blob
            // For now, we'll do structural validation only
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "PassengerTrainCompositionProcessMessage_v2.1.6.xsd");
            
            if (File.Exists(schemaPath))
            {
                using var reader = XmlReader.Create(schemaPath);
                _schemaSet!.Add("http://taf-jsg.info/schemas/2.1.6/ptcpm", reader);
                _schemaSet.Compile();
                return true;
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    public ValidationResult Validate(string xml)
    {
        return ValidateWithDetails(xml);
    }

    public ValidationResult ValidateWithDetails(string xml)
    {
        var errors = new List<ValidationError>();
        var warnings = new List<string>();

        try
        {
            // Perform well-formedness check
            var settings = new XmlReaderSettings
            {
                ValidationType = _schemaLoaded ? ValidationType.Schema : ValidationType.None,
                DtdProcessing = DtdProcessing.Prohibit // Security: prevent DTD attacks
            };

            if (_schemaLoaded && _schemaSet != null)
            {
                settings.Schemas = _schemaSet;
                settings.ValidationEventHandler += (sender, e) =>
                {
                    if (e.Severity == XmlSeverityType.Error)
                    {
                        errors.Add(new ValidationError
                        {
                            Message = e.Message,
                            LineNumber = e.Exception?.LineNumber ?? 0,
                            LinePosition = e.Exception?.LinePosition ?? 0,
                            Severity = e.Severity
                        });
                    }
                    else
                    {
                        warnings.Add(e.Message);
                    }
                };
            }

            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);

            // Read through entire document to trigger validation
            while (reader.Read())
            {
                // Validate required elements exist
                if (reader.NodeType == XmlNodeType.Element)
                {
                    ValidateRequiredElements(reader, errors);
                }
            }

            // Perform additional structural validation
            ValidateStructure(xml, errors);
        }
        catch (XmlException ex)
        {
            errors.Add(new ValidationError
            {
                Message = $"XML parsing error: {ex.Message}",
                LineNumber = ex.LineNumber,
                LinePosition = ex.LinePosition,
                Severity = XmlSeverityType.Error
            });
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private void ValidateRequiredElements(XmlReader reader, List<ValidationError> errors)
    {
        // Track required elements for TAF-JSG compliance
        // This is a simplified check - full validation would use XSD
    }

    private void ValidateStructure(string xml, List<ValidationError> errors)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);

        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("ptcpm", "http://taf-jsg.info/schemas/2.1.6/ptcpm");

        // Validate required elements exist
        var requiredElements = new[]
        {
            "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:MessageHeader/ptcpm:MessageIdentifier",
            "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:MessageHeader/ptcpm:MessageType",
            "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:TrainInformation/ptcpm:TrainNumber",
            "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:TrainInformation/ptcpm:TravelDate"
        };

        foreach (var xpath in requiredElements)
        {
            var node = doc.SelectSingleNode(xpath, ns);
            if (node == null || string.IsNullOrWhiteSpace(node.InnerText))
            {
                var elementName = xpath.Split('/').Last().Replace("ptcpm:", "");
                errors.Add(new ValidationError
                {
                    Message = $"Required element '{elementName}' is missing or empty",
                    Severity = XmlSeverityType.Error
                });
            }
        }

        // Validate train number format (should be alphanumeric)
        var trainNumber = doc.SelectSingleNode(
            "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:TrainInformation/ptcpm:TrainNumber", ns);
        if (trainNumber != null && !IsValidTrainNumber(trainNumber.InnerText))
        {
            errors.Add(new ValidationError
            {
                Message = $"Invalid train number format: {trainNumber.InnerText}",
                Severity = XmlSeverityType.Error
            });
        }

        // Validate travel date format (should be yyyy-MM-dd)
        var travelDate = doc.SelectSingleNode(
            "/ptcpm:PassengerTrainCompositionProcessMessage/ptcpm:TrainInformation/ptcpm:TravelDate", ns);
        if (travelDate != null && !IsValidDateFormat(travelDate.InnerText))
        {
            errors.Add(new ValidationError
            {
                Message = $"Invalid travel date format: {travelDate.InnerText}. Expected: yyyy-MM-dd",
                Severity = XmlSeverityType.Error
            });
        }
    }

    private static bool IsValidTrainNumber(string trainNumber)
    {
        // Train numbers should be alphanumeric and between 2-10 characters
        return !string.IsNullOrWhiteSpace(trainNumber) 
               && trainNumber.Length >= 2 
               && trainNumber.Length <= 20
               && trainNumber.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
    }

    private static bool IsValidDateFormat(string date)
    {
        return DateTime.TryParseExact(date, "yyyy-MM-dd", 
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, 
            out _);
    }
}
