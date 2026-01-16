using System.Text;
using System.Xml;
using Transgrid.Functions.Models;

namespace Transgrid.Functions.Services;

/// <summary>
/// Service for transforming JSON train plan data to TAF-JSG XML format.
/// </summary>
public interface IXmlTransformService
{
    /// <summary>
    /// Transforms a train plan to TAF-JSG PassengerTrainCompositionProcessMessage XML.
    /// </summary>
    string Transform(TrainPlanInput trainPlan, ReferenceData referenceData);
}

/// <summary>
/// Implementation of XML transformation to TAF-JSG v2.1.6 format.
/// </summary>
public class XmlTransformService : IXmlTransformService
{
    private const string TafJsgNamespace = "http://taf-jsg.info/schemas/2.1.6/ptcpm";
    private const string SenderReference = "EUROSTAR";
    private const string RecipientReference = "RNE";

    public string Transform(TrainPlanInput trainPlan, ReferenceData referenceData)
    {
        ArgumentNullException.ThrowIfNull(trainPlan);
        ArgumentNullException.ThrowIfNull(referenceData);

        if (string.IsNullOrWhiteSpace(trainPlan.ServiceCode))
        {
            throw new ArgumentException("ServiceCode is required", nameof(trainPlan));
        }

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = new UTF8Encoding(false), // UTF-8 without BOM
            OmitXmlDeclaration = false
        };

        using var stringWriter = new Utf8StringWriter();
        using var writer = XmlWriter.Create(stringWriter, settings);

        WriteDocument(writer, trainPlan, referenceData);

        writer.Flush();
        return stringWriter.ToString();
    }

    private void WriteDocument(XmlWriter writer, TrainPlanInput trainPlan, ReferenceData referenceData)
    {
        writer.WriteStartDocument();
        
        // Root element with namespace
        writer.WriteStartElement("PassengerTrainCompositionProcessMessage", TafJsgNamespace);
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
        writer.WriteAttributeString("xsi", "schemaLocation", null, 
            $"{TafJsgNamespace} PassengerTrainCompositionProcessMessage_v2.1.6.xsd");

        WriteMessageHeader(writer, trainPlan);
        WriteTrainInformation(writer, trainPlan, referenceData);

        writer.WriteEndElement(); // PassengerTrainCompositionProcessMessage
        writer.WriteEndDocument();
    }

    private void WriteMessageHeader(XmlWriter writer, TrainPlanInput trainPlan)
    {
        writer.WriteStartElement("MessageHeader");

        // Generate unique message identifier
        var messageId = $"EUR-{trainPlan.ServiceCode}-{trainPlan.TravelDate}-{Guid.NewGuid():N}".Substring(0, 50);
        writer.WriteElementString("MessageIdentifier", messageId);
        writer.WriteElementString("MessageType", "PTCPMRequest");
        writer.WriteElementString("MessageVersion", "2.1.6");
        writer.WriteElementString("SenderReference", SenderReference);
        writer.WriteElementString("RecipientReference", RecipientReference);
        writer.WriteElementString("MessageDateTime", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        writer.WriteElementString("MessageSequenceNumber", "1");

        writer.WriteEndElement(); // MessageHeader
    }

    private void WriteTrainInformation(XmlWriter writer, TrainPlanInput trainPlan, ReferenceData referenceData)
    {
        writer.WriteStartElement("TrainInformation");

        // Core train identification
        writer.WriteElementString("TrainNumber", trainPlan.ServiceCode);
        writer.WriteElementString("TravelDate", trainPlan.TravelDate);
        writer.WriteElementString("OperationalPathway", trainPlan.Pathway);
        
        // Origin and destination with UIC codes
        WriteLocation(writer, "Origin", trainPlan.Origin, referenceData);
        WriteLocation(writer, "Destination", trainPlan.Destination, referenceData);

        // Train type information
        writer.WriteElementString("TrainType", "PASSENGER");
        writer.WriteElementString("TrainCategory", "HIGH_SPEED");
        writer.WriteElementString("OperatingCarrier", "EUROSTAR");

        // Write passage points (journey path)
        WritePassagePoints(writer, trainPlan, referenceData);

        // Train composition (generic for now)
        WriteTrainComposition(writer, trainPlan);

        writer.WriteEndElement(); // TrainInformation
    }

    private void WriteLocation(XmlWriter writer, string elementName, string locationCode, ReferenceData referenceData)
    {
        writer.WriteStartElement(elementName);
        writer.WriteElementString("LocationCode", locationCode);
        
        var uicCode = referenceData.Locations.TryGetValue(locationCode, out var loc) 
            ? loc.UicCode 
            : $"0000{locationCode}";
        writer.WriteElementString("LocationUICCode", uicCode);

        if (loc != null)
        {
            writer.WriteElementString("LocationName", loc.Name);
            writer.WriteElementString("Country", loc.Country);
        }

        writer.WriteEndElement();
    }

    private void WritePassagePoints(XmlWriter writer, TrainPlanInput trainPlan, ReferenceData referenceData)
    {
        writer.WriteStartElement("PassagePoints");

        int sequence = 1;
        foreach (var point in trainPlan.PassagePoints)
        {
            writer.WriteStartElement("PassagePoint");
            
            writer.WriteElementString("Sequence", sequence.ToString());
            writer.WriteElementString("LocationCode", point.LocationCode);
            
            var uicCode = referenceData.Locations.TryGetValue(point.LocationCode, out var loc) 
                ? loc.UicCode 
                : $"0000{point.LocationCode}";
            writer.WriteElementString("LocationUICCode", uicCode);

            if (loc != null)
            {
                writer.WriteElementString("LocationName", loc.Name);
                writer.WriteElementString("LocationType", loc.Type);
            }

            // Times
            if (!string.IsNullOrEmpty(point.ArrivalTime))
            {
                writer.WriteElementString("ArrivalTime", point.ArrivalTime);
            }
            if (!string.IsNullOrEmpty(point.DepartureTime))
            {
                writer.WriteElementString("DepartureTime", point.DepartureTime);
            }

            // Activity at this point
            var isFirstOrLast = sequence == 1 || sequence == trainPlan.PassagePoints.Count;
            writer.WriteElementString("ActivityType", isFirstOrLast ? "TERMINAL" : "TRANSIT");

            writer.WriteEndElement(); // PassagePoint
            sequence++;
        }

        writer.WriteEndElement(); // PassagePoints
    }

    private void WriteTrainComposition(XmlWriter writer, TrainPlanInput trainPlan)
    {
        writer.WriteStartElement("TrainComposition");
        
        writer.WriteElementString("TrainUnitType", "E320"); // Eurostar e320
        writer.WriteElementString("NumberOfCars", "16");
        writer.WriteElementString("TotalLength", "394"); // meters
        writer.WriteElementString("MaxSpeed", "320"); // km/h
        writer.WriteElementString("TareWeight", "965"); // tonnes
        
        // Passenger capacity
        writer.WriteStartElement("PassengerCapacity");
        writer.WriteElementString("FirstClass", "206");
        writer.WriteElementString("StandardClass", "544");
        writer.WriteElementString("StandardPremier", "150");
        writer.WriteElementString("Total", "900");
        writer.WriteEndElement();

        writer.WriteEndElement(); // TrainComposition
    }

    /// <summary>
    /// Custom StringWriter that uses UTF-8 encoding.
    /// </summary>
    private class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
