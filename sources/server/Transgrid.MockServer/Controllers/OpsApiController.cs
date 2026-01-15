using Microsoft.AspNetCore.Mvc;
using Transgrid.MockServer.Models;
using Transgrid.MockServer.Services;

namespace Transgrid.MockServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OpsApiController : ControllerBase
{
    private readonly DataStore _dataStore;

    public OpsApiController(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    [HttpGet]
    public ActionResult<List<TrainPlan>> GetAll()
    {
        return Ok(_dataStore.GetTrainPlans());
    }

    [HttpGet("{id}")]
    public ActionResult<TrainPlan> Get(string id)
    {
        var plan = _dataStore.GetTrainPlan(id);
        if (plan == null)
        {
            return NotFound();
        }
        return Ok(plan);
    }

    [HttpPost]
    public ActionResult<TrainPlan> Create([FromBody] TrainPlan? plan)
    {
        if (plan == null)
        {
            return BadRequest("Train plan data is required");
        }
        
        if (string.IsNullOrWhiteSpace(plan.ServiceCode))
        {
            return BadRequest("Service code is required");
        }
        
        plan.Id = Guid.NewGuid().ToString();
        plan.CreatedAt = DateTime.UtcNow;
        _dataStore.AddTrainPlan(plan);
        return CreatedAtAction(nameof(Get), new { id = plan.Id }, plan);
    }

    [HttpPut("{id}")]
    public ActionResult Update(string id, [FromBody] TrainPlan? plan)
    {
        if (plan == null)
        {
            return BadRequest("Train plan data is required");
        }
        
        if (string.IsNullOrWhiteSpace(plan.ServiceCode))
        {
            return BadRequest("Service code is required");
        }
        
        var existing = _dataStore.GetTrainPlan(id);
        if (existing == null)
        {
            return NotFound();
        }
        plan.Id = id;
        plan.CreatedAt = existing.CreatedAt;
        _dataStore.UpdateTrainPlan(plan);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(string id)
    {
        var existing = _dataStore.GetTrainPlan(id);
        if (existing == null)
        {
            return NotFound();
        }
        _dataStore.DeleteTrainPlan(id);
        return NoContent();
    }
}
