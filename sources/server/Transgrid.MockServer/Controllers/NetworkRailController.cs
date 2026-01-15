using Microsoft.AspNetCore.Mvc;
using Transgrid.MockServer.Models;
using Transgrid.MockServer.Services;

namespace Transgrid.MockServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NetworkRailController : ControllerBase
{
    private readonly DataStore _dataStore;

    public NetworkRailController(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    [HttpGet]
    public ActionResult<List<CifSchedule>> GetAll()
    {
        return Ok(_dataStore.GetCifSchedules());
    }

    [HttpGet("{id}")]
    public ActionResult<CifSchedule> Get(string id)
    {
        var schedule = _dataStore.GetCifSchedule(id);
        if (schedule == null)
        {
            return NotFound();
        }
        return Ok(schedule);
    }

    [HttpPost]
    public ActionResult<CifSchedule> Create([FromBody] CifSchedule? schedule)
    {
        if (schedule == null)
        {
            return BadRequest("CIF schedule data is required");
        }
        
        if (string.IsNullOrWhiteSpace(schedule.TrainServiceNumber))
        {
            return BadRequest("Train service number is required");
        }
        
        schedule.Id = Guid.NewGuid().ToString();
        schedule.CreatedAt = DateTime.UtcNow;
        _dataStore.AddCifSchedule(schedule);
        return CreatedAtAction(nameof(Get), new { id = schedule.Id }, schedule);
    }

    [HttpPut("{id}")]
    public ActionResult Update(string id, [FromBody] CifSchedule? schedule)
    {
        if (schedule == null)
        {
            return BadRequest("CIF schedule data is required");
        }
        
        if (string.IsNullOrWhiteSpace(schedule.TrainServiceNumber))
        {
            return BadRequest("Train service number is required");
        }
        
        var existing = _dataStore.GetCifSchedule(id);
        if (existing == null)
        {
            return NotFound();
        }
        schedule.Id = id;
        schedule.CreatedAt = existing.CreatedAt;
        _dataStore.UpdateCifSchedule(schedule);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(string id)
    {
        var existing = _dataStore.GetCifSchedule(id);
        if (existing == null)
        {
            return NotFound();
        }
        _dataStore.DeleteCifSchedule(id);
        return NoContent();
    }
}
