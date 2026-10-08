using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Services;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(
            IReportService reportService,
            UserManager<ApplicationUser> userManager)
        {
            _reportService = reportService;
            _userManager = userManager;
        }

        private async Task<(string UserId, string Role)> GetUserContextAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            var roles = user != null ? await _userManager.GetRolesAsync(user) : new string[0];
            return (user?.Id ?? string.Empty, roles.Count > 0 ? roles[0] : "SalesExecutive");
        }

        // GET: /Reports
        public async Task<IActionResult> Index()
        {
            var (userId, role) = await GetUserContextAsync();
            var pipeline = await _reportService.GetPipelineReportAsync(userId, role);
            var conversion = await _reportService.GetConversionReportAsync(userId, role);

            ViewBag.Pipeline = pipeline;
            ViewBag.Conversion = conversion;
            ViewBag.UserRole = role;

            return View();
        }

        // GET: /Reports/Customers
        public async Task<IActionResult> Customers(string? status, DateTime? fromDate, DateTime? toDate)
        {
            var (userId, role) = await GetUserContextAsync();
            var list = await _reportService.GetCustomerReportAsync(status, fromDate, toDate, userId, role);
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            return View(list);
        }

        // GET: /Reports/Leads
        public async Task<IActionResult> Leads(string? status, string? source, DateTime? fromDate, DateTime? toDate)
        {
            var (userId, role) = await GetUserContextAsync();
            var list = await _reportService.GetLeadReportAsync(status, source, fromDate, toDate, userId, role);
            ViewBag.Status = status;
            ViewBag.Source = source;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            return View(list);
        }

        // GET: /Reports/Opportunities
        public async Task<IActionResult> Opportunities(string? stage, DateTime? fromDate, DateTime? toDate)
        {
            var (userId, role) = await GetUserContextAsync();
            var list = await _reportService.GetOpportunityReportAsync(stage, fromDate, toDate, userId, role);
            ViewBag.Stage = stage;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            return View(list);
        }

        // GET: /Reports/FollowUps
        public async Task<IActionResult> FollowUps(string? status, string? type, DateTime? fromDate, DateTime? toDate)
        {
            var (userId, role) = await GetUserContextAsync();
            var list = await _reportService.GetFollowUpReportAsync(status, type, fromDate, toDate, userId, role);
            ViewBag.Status = status;
            ViewBag.Type = type;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            return View(list);
        }

        // GET: /Reports/UserActivity
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> UserActivity()
        {
            var report = await _reportService.GetUserActivityReportAsync();
            return View(report);
        }

        // GET: /Reports/ExportCsv
        public async Task<IActionResult> ExportCsv(string type, string? status, string? stage, string? source, DateTime? fromDate, DateTime? toDate)
        {
            var (userId, role) = await GetUserContextAsync();
            var sb = new StringBuilder();
            var fileName = $"Report_{type}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            switch (type?.ToLower())
            {
                case "customers":
                    var custs = await _reportService.GetCustomerReportAsync(status, fromDate, toDate, userId, role);
                    sb.AppendLine("CustomerCode,CustomerName,Email,Phone,Company,Status,CreatedDate,Owner");
                    foreach (var c in custs)
                    {
                        sb.AppendLine($"\"{c.CustomerCode}\",\"{c.CustomerName}\",\"{c.Email}\",\"{c.Phone}\",\"{c.CompanyName}\",\"{c.Status}\",\"{c.CreatedDate:yyyy-MM-dd}\",\"{c.Owner?.FullName ?? c.Owner?.UserName}\"");
                    }
                    break;

                case "leads":
                    var leads = await _reportService.GetLeadReportAsync(status, source, fromDate, toDate, userId, role);
                    sb.AppendLine("LeadCode,LeadName,Email,Phone,Company,Source,Status,Priority,ExpectedValue,AssignedTo");
                    foreach (var l in leads)
                    {
                        sb.AppendLine($"\"{l.LeadCode}\",\"{l.LeadName}\",\"{l.Email}\",\"{l.Phone}\",\"{l.CompanyName}\",\"{l.Source}\",\"{l.Status}\",\"{l.Priority}\",{l.ExpectedValue},\"{l.AssignedUser?.FullName ?? l.AssignedUser?.UserName}\"");
                    }
                    break;

                case "opportunities":
                    var opps = await _reportService.GetOpportunityReportAsync(stage, fromDate, toDate, userId, role);
                    sb.AppendLine("OpportunityId,OpportunityName,Customer,Amount,Stage,Probability,WeightedValue,ExpectedCloseDate,AssignedTo");
                    foreach (var o in opps)
                    {
                        sb.AppendLine($"{o.OpportunityId},\"{o.OpportunityName}\",\"{o.Customer?.CustomerName}\",{o.Amount},\"{o.Stage}\",{o.Probability},{o.WeightedAmount},\"{o.ExpectedCloseDate:yyyy-MM-dd}\",\"{o.AssignedUser?.FullName ?? o.AssignedUser?.UserName}\"");
                    }
                    break;

                case "pipeline":
                    var pipeline = await _reportService.GetPipelineReportAsync(userId, role);
                    sb.AppendLine("Stage,DealCount,TotalAmount,WeightedAmount");
                    foreach (var p in pipeline)
                    {
                        sb.AppendLine($"\"{p.Stage}\",{p.OpportunityCount},{p.TotalAmount},{p.WeightedAmount}");
                    }
                    break;

                default:
                    return BadRequest("Invalid report type specified.");
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", fileName);
        }
    }
}
