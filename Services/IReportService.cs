using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public class PipelineReportItem
    {
        public string Stage { get; set; } = string.Empty;
        public int OpportunityCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
    }

    public class ConversionReportItem
    {
        public int TotalLeads { get; set; }
        public int ConvertedLeads { get; set; }
        public decimal LeadConversionRate { get; set; }
        public int TotalOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public decimal OpportunityWinRate { get; set; }
        public decimal TotalWonRevenue { get; set; }
    }

    public class UserActivityReportItem
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int TotalActions { get; set; }
        public int LoginsCount { get; set; }
        public int CreatesCount { get; set; }
        public int UpdatesCount { get; set; }
        public int DeletesCount { get; set; }
        public DateTime? LastActiveDate { get; set; }
    }

    public interface IReportService
    {
        Task<List<Customer>> GetCustomerReportAsync(string? status, DateTime? fromDate, DateTime? toDate, string? currentUserId, string? role);
        Task<List<Lead>> GetLeadReportAsync(string? status, string? source, DateTime? fromDate, DateTime? toDate, string? currentUserId, string? role);
        Task<List<Opportunity>> GetOpportunityReportAsync(string? stage, DateTime? fromDate, DateTime? toDate, string? currentUserId, string? role);
        Task<List<FollowUp>> GetFollowUpReportAsync(string? status, string? type, DateTime? fromDate, DateTime? toDate, string? currentUserId, string? role);
        Task<List<PipelineReportItem>> GetPipelineReportAsync(string? currentUserId, string? role);
        Task<ConversionReportItem> GetConversionReportAsync(string? currentUserId, string? role);
        Task<List<UserActivityReportItem>> GetUserActivityReportAsync();
    }
}
