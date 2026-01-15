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
