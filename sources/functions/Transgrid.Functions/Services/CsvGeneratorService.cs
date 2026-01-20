using System.Text;
using Transgrid.Functions.Models;

namespace Transgrid.Functions.Services;

/// <summary>
/// Service for generating CSV files from negotiated rate data.
/// Implements the three extract routes: IDL/S3, GDS Air, and BeNe.
/// </summary>
public interface ICsvGeneratorService
{
    /// <summary>
    /// Generates CSV content for the IDL/S3 (Internal Distribution) route.
    /// </summary>
    string GenerateIdlS3Csv(IEnumerable<NegotiatedRateInput> rates);

    /// <summary>
    /// Generates CSV content for the GDS Air (Travel Agents) route.
    /// </summary>
    string GenerateGdsAirCsv(IEnumerable<NegotiatedRateInput> rates);

    /// <summary>
    /// Generates CSV content for the BeNe (External Partners) route.
    /// </summary>
    string GenerateBeneCsv(IEnumerable<NegotiatedRateInput> rates);

    /// <summary>
    /// Filters rates for the IDL/S3 route.
    /// </summary>
    IEnumerable<NegotiatedRateInput> FilterForIdlS3(IEnumerable<NegotiatedRateInput> rates);

    /// <summary>
    /// Filters rates for the GDS Air route.
    /// </summary>
    IEnumerable<NegotiatedRateInput> FilterForGdsAir(IEnumerable<NegotiatedRateInput> rates);

    /// <summary>
    /// Filters rates for the BeNe route.
    /// </summary>
    IEnumerable<NegotiatedRateInput> FilterForBeNe(IEnumerable<NegotiatedRateInput> rates);
}

/// <summary>
/// Implementation of CSV generator service for Salesforce negotiated rates.
/// </summary>
public class CsvGeneratorService : ICsvGeneratorService
{
    private static readonly string[] IdlS3RecordTypes = { "GND BE", "GND NL", "FCE", "IDL" };
    private static readonly string[] GdsAirRecordTypes = { "Amadeus", "Apollo", "Galileo", "Sabre" };
    private static readonly string[] BeNeRecordTypes = { "GND BE", "GND NL" };

    public IEnumerable<NegotiatedRateInput> FilterForIdlS3(IEnumerable<NegotiatedRateInput> rates)
    {
        return rates.Where(r => 
            IdlS3RecordTypes.Any(t => r.CodeRecordType.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    public IEnumerable<NegotiatedRateInput> FilterForGdsAir(IEnumerable<NegotiatedRateInput> rates)
    {
        return rates.Where(r => 
            !string.IsNullOrWhiteSpace(r.GdsUsed) && 
            GdsAirRecordTypes.Any(t => r.GdsUsed.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    public IEnumerable<NegotiatedRateInput> FilterForBeNe(IEnumerable<NegotiatedRateInput> rates)
    {
        return rates.Where(r => 
            !string.IsNullOrWhiteSpace(r.Distributor) && 
            BeNeRecordTypes.Any(t => r.CodeRecordType.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    public string GenerateIdlS3Csv(IEnumerable<NegotiatedRateInput> rates)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("Account Manager,Account Name,Unique Code,Type,Road,Tariff Codes,Discounts,Action Type");
        
        foreach (var rate in rates)
        {
            var tariffCodes = string.Join("|", rate.TariffCodes);
            var discounts = string.Join("|", rate.Discounts.Select(d => $"{d.Value}%"));
            
            sb.AppendLine($"{EscapeCsv(rate.AccountManager)},{EscapeCsv(rate.AccountName)},{EscapeCsv(rate.UniqueCode)},{EscapeCsv(rate.CodeRecordType)},{EscapeCsv(rate.Road)},{EscapeCsv(tariffCodes)},{EscapeCsv(discounts)},{EscapeCsv(rate.ActionType)}");
        }
        
        return sb.ToString();
    }

    public string GenerateGdsAirCsv(IEnumerable<NegotiatedRateInput> rates)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("Account Manager,Account Name,Unique Code,GDS Used,PCC,Road,Tariff Codes,Valid From,Valid To,Action Type");
        
        foreach (var rate in rates)
        {
            var tariffCodes = string.Join("|", rate.TariffCodes);
            var validFrom = rate.ValidFrom?.ToString("yyyy-MM-dd") ?? "";
            var validTo = rate.ValidTo?.ToString("yyyy-MM-dd") ?? "";
            
            sb.AppendLine($"{EscapeCsv(rate.AccountManager)},{EscapeCsv(rate.AccountName)},{EscapeCsv(rate.UniqueCode)},{EscapeCsv(rate.GdsUsed ?? "")},{EscapeCsv(rate.Pcc ?? "")},{EscapeCsv(rate.Road)},{EscapeCsv(tariffCodes)},{validFrom},{validTo},{EscapeCsv(rate.ActionType)}");
        }
        
        return sb.ToString();
    }

    public string GenerateBeneCsv(IEnumerable<NegotiatedRateInput> rates)
    {
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("Account Manager,Account Name,Unique Code,Distributor,Road,Tariff Codes,Action Type");
        
        foreach (var rate in rates)
        {
            var tariffCodes = string.Join("|", rate.TariffCodes);
            
            sb.AppendLine($"{EscapeCsv(rate.AccountManager)},{EscapeCsv(rate.AccountName)},{EscapeCsv(rate.UniqueCode)},{EscapeCsv(rate.Distributor ?? "")},{EscapeCsv(rate.Road)},{EscapeCsv(tariffCodes)},{EscapeCsv(rate.ActionType)}");
        }
        
        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
            
        // If value contains comma, quote, or newline, wrap in quotes and escape quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        
        return value;
    }
}
