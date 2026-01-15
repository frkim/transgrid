namespace Transgrid.MockServer.Models;

public class CifSchedule
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TrainServiceNumber { get; set; } = string.Empty;
    public DateTime TravelDate { get; set; }
    public string CifStpIndicator { get; set; } = "N";
    public List<ScheduleLocation> ScheduleLocations { get; set; } = new();
    public string TrainCategory { get; set; } = string.Empty;
    public string PowerType { get; set; } = string.Empty;
    public string TrainClass { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ScheduleLocation
{
    public string LocationCode { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string ScheduledArrivalTime { get; set; } = string.Empty;
    public string ScheduledDepartureTime { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Activity { get; set; } = string.Empty;
}
