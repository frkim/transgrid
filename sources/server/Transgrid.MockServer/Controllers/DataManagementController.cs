using Microsoft.AspNetCore.Mvc;
using Transgrid.MockServer.Services;

namespace Transgrid.MockServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DataManagementController : ControllerBase
{
    private readonly DataStore _dataStore;

    public DataManagementController(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    [HttpPost("generate")]
    public ActionResult GenerateNewData()
    {
        _dataStore.GenerateNewData();
        return Ok(new { message = "New data generated successfully" });
    }

    /// <summary>
    /// Generate deterministic test data for workflow validation.
    /// Creates 8 train plans for today (FR/GB, ACTIVE, not EVOLUTION) that will pass filters.
    /// Also creates 6 train plans for D+2 and 4 plans that should be excluded.
    /// </summary>
    [HttpPost("generate-test-data")]
    public ActionResult GenerateTestData()
    {
        _dataStore.GenerateTestDataForWorkflowValidation();
        
        var plans = _dataStore.GetTrainPlans();
        var today = DateTime.Today;
        var d2Date = today.AddDays(2);
        
        var todayPlans = plans.Where(p => p.TravelDate.Date == today).ToList();
        var d2Plans = plans.Where(p => p.TravelDate.Date == d2Date).ToList();
        var activeFrGbToday = todayPlans.Where(p => 
            (p.Country == "FR" || p.Country == "GB") && 
            p.Status == "ACTIVE" && 
            p.PlanType != "EVOLUTION").ToList();
        var activeFrGbD2 = d2Plans.Where(p => 
            (p.Country == "FR" || p.Country == "GB") && 
            p.Status == "ACTIVE" && 
            p.PlanType != "EVOLUTION").ToList();
        
        return Ok(new 
        { 
            message = "Test data generated for workflow validation",
            summary = new
            {
                totalPlans = plans.Count,
                todayDate = today.ToString("yyyy-MM-dd"),
                d2Date = d2Date.ToString("yyyy-MM-dd"),
                todayPlansTotal = todayPlans.Count,
                todayPlansMatchingFilters = activeFrGbToday.Count,
                d2PlansTotal = d2Plans.Count,
                d2PlansMatchingFilters = activeFrGbD2.Count,
                todayServiceCodes = activeFrGbToday.Select(p => p.ServiceCode).OrderBy(x => x).ToList(),
                d2ServiceCodes = activeFrGbD2.Select(p => p.ServiceCode).OrderBy(x => x).ToList()
            }
        });
    }

    [HttpPost("reset")]
    public ActionResult ResetToBaseline()
    {
        _dataStore.ResetToBaseline();
        return Ok(new { message = "Data reset to baseline successfully" });
    }

    [HttpPost("update")]
    public ActionResult UpdateData([FromBody] UpdateDataRequest request)
    {
        if (request == null || request.PeriodMinutes <= 0)
        {
            return BadRequest(new { message = "Invalid period specified" });
        }
        
        _dataStore.UpdateData(request.PeriodMinutes);
        return Ok(new { message = $"Data updated with {request.PeriodMinutes} minute period" });
    }
}

public class UpdateDataRequest
{
    public int PeriodMinutes { get; set; }
}
