using Microsoft.AspNetCore.Mvc;
using Transgrid.MockServer.Models;
using Transgrid.MockServer.Services;

namespace Transgrid.MockServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesforceController : ControllerBase
{
    private readonly DataStore _dataStore;

    public SalesforceController(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    [HttpGet]
    public ActionResult<List<NegotiatedRate>> GetAll()
    {
        return Ok(_dataStore.GetNegotiatedRates());
    }

    [HttpGet("{id}")]
    public ActionResult<NegotiatedRate> Get(string id)
    {
        var rate = _dataStore.GetNegotiatedRate(id);
        if (rate == null)
        {
            return NotFound();
        }
        return Ok(rate);
    }

    [HttpPost]
    public ActionResult<NegotiatedRate> Create([FromBody] NegotiatedRate rate)
    {
        rate.Id = Guid.NewGuid().ToString();
        rate.CreatedAt = DateTime.UtcNow;
        _dataStore.AddNegotiatedRate(rate);
        return CreatedAtAction(nameof(Get), new { id = rate.Id }, rate);
    }

    [HttpPut("{id}")]
    public ActionResult Update(string id, [FromBody] NegotiatedRate rate)
    {
        var existing = _dataStore.GetNegotiatedRate(id);
        if (existing == null)
        {
            return NotFound();
        }
        rate.Id = id;
        _dataStore.UpdateNegotiatedRate(rate);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public ActionResult Delete(string id)
    {
        var existing = _dataStore.GetNegotiatedRate(id);
        if (existing == null)
        {
            return NotFound();
        }
        _dataStore.DeleteNegotiatedRate(id);
        return NoContent();
    }
}
