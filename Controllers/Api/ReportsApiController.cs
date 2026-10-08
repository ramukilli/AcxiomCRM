using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/reports")]
    [Produces("application/json")]
    public class ReportsApiController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportsApiController(IReportService reportService)
        {
            _reportService = reportService;
        }

        // GET: /api/reports/pipeline
        [HttpGet("pipeline")]
        public async Task<ActionResult<IEnumerable<PipelineReportItem>>> GetPipelineReport()
        {
            var data = await _reportService.GetPipelineReportAsync(null, "Admin");
            return Ok(data);
        }
    }
}
