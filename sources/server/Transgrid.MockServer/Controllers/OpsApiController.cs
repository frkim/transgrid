using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
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

    /// <summary>
    /// GraphQL-like endpoint for querying train plans.
    /// Accepts a GraphQL-style query and returns filtered results.
    /// </summary>
    /// <remarks>
    /// Example request body:
    /// {
    ///   "query": "query GetTrainPlans($travelDate: Date!) { trainPlans(filter: { travelDate: $travelDate }) { ... } }",
    ///   "variables": { "travelDate": "2026-01-16" }
    /// }
    /// </remarks>
    [HttpPost("/graphql")]
    public ActionResult<object> GraphQLQuery([FromBody] GraphQLRequest? request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { errors = new[] { new { message = "Query is required" } } });
        }

        try
        {
            // Parse variables
            DateTime? travelDate = null;
            string? trainId = null;

            if (request.Variables != null)
            {
                if (request.Variables.TryGetValue("travelDate", out var travelDateValue))
                {
                    if (travelDateValue is JsonElement jsonElement)
                    {
                        travelDate = DateTime.Parse(jsonElement.GetString() ?? string.Empty);
                    }
                    else if (travelDateValue is string dateStr)
                    {
                        travelDate = DateTime.Parse(dateStr);
                    }
                }

                if (request.Variables.TryGetValue("id", out var idValue))
                {
                    if (idValue is JsonElement jsonElement)
                    {
                        trainId = jsonElement.GetString();
                    }
                    else if (idValue is string idStr)
                    {
                        trainId = idStr;
                    }
                }
            }

            // Handle single train plan query
            if (request.Query.Contains("trainPlan(id:") || request.Query.Contains("GetTrainPlanById"))
            {
                if (!string.IsNullOrEmpty(trainId))
                {
                    var plan = _dataStore.GetTrainPlan(trainId);
                    if (plan == null)
                    {
                        // Try finding by service code
                        plan = _dataStore.GetTrainPlans()
                            .FirstOrDefault(p => p.ServiceCode.Equals(trainId, StringComparison.OrdinalIgnoreCase));
                    }

                    return Ok(new
                    {
                        data = new
                        {
                            trainPlan = plan
                        }
                    });
                }
            }

            // Handle train plans list query
            var trainPlans = _dataStore.GetTrainPlans();

            // Filter by travel date if provided
            if (travelDate.HasValue)
            {
                trainPlans = trainPlans
                    .Where(p => p.TravelDate.Date == travelDate.Value.Date)
                    .ToList();
            }

            return Ok(new
            {
                data = new
                {
                    trainPlans = trainPlans.Select(p => new
                    {
                        p.Id,
                        p.ServiceCode,
                        p.Pathway,
                        travelDate = p.TravelDate.ToString("yyyy-MM-dd"),
                        passagePoints = p.PassagePoints.Select(pp => new
                        {
                            locationCode = pp,
                            arrivalTime = (string?)null,
                            departureTime = (string?)null
                        }),
                        p.Origin,
                        p.Destination,
                        p.Status,
                        p.PlanType,
                        p.Country
                    })
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                errors = new[] { new { message = ex.Message } }
            });
        }
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
