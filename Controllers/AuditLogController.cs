using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class AuditLogController : Controller
    {
        private readonly IAuditService _auditService;

        public AuditLogController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        // GET: /AuditLog
        public async Task<IActionResult> Index(
            string? user,
            string? module,
            string? action,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var logs = await _auditService.GetLogsAsync(user, module, action, fromDate, toDate);

            ViewBag.CurrentUser = user;
            ViewBag.CurrentModule = module;
            ViewBag.CurrentAction = action;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(logs);
        }

        // GET: /AuditLog/ExportCsv
        public async Task<IActionResult> ExportCsv(
            string? user,
            string? module,
            string? action,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var logs = await _auditService.GetLogsAsync(user, module, action, fromDate, toDate);

            var sb = new StringBuilder();
            sb.AppendLine("AuditLogId,Timestamp,UserName,Action,Module,RecordId,Result,IpAddress,Details");

            foreach (var log in logs)
            {
                var cleanDetails = (log.Details ?? string.Empty).Replace("\"", "\"\"");
                sb.AppendLine($"{log.AuditLogId},\"{log.CreatedDate:yyyy-MM-dd HH:mm:ss}\",\"{log.UserName}\",\"{log.Action}\",\"{log.EntityName}\",\"{log.RecordId}\",\"{log.Result}\",\"{log.IpAddress}\",\"{cleanDetails}\"");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"AcxiomCRM_AuditLog_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }
    }
}
