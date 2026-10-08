using System.Collections.Generic;
using System.Threading.Tasks;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public interface ILeadService
    {
        Task<List<Lead>> GetAllAsync(string? search = null, string? status = null, string? assignedTo = null, string? currentUserId = null, string? role = null);
        Task<Lead?> GetByIdAsync(int id);
        Task<string> GenerateLeadCodeAsync();
        Task<(bool Success, string? Error, Lead? Lead)> CreateAsync(Lead lead, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error)> UpdateAsync(Lead lead, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error)> DeleteAsync(int id, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error, int? CustomerId, int? OpportunityId)> ConvertLeadAsync(int leadId, string currentUserId, string? currentUserName, string? ipAddress);
    }
}
