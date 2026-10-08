using System.Collections.Generic;
using System.Threading.Tasks;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public interface IOpportunityService
    {
        Task<List<Opportunity>> GetAllAsync(string? search = null, string? stage = null, string? status = null, string? currentUserId = null, string? role = null);
        Task<Opportunity?> GetByIdAsync(int id);
        Task<(bool Success, string? Error, Opportunity? Opportunity)> CreateAsync(Opportunity opportunity, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error)> UpdateAsync(Opportunity opportunity, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error)> DeleteAsync(int id, string currentUserId, string? currentUserName, string? ipAddress);
    }
}
