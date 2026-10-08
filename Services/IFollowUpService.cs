using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public interface IFollowUpService
    {
        Task<List<FollowUp>> GetAllAsync(string? status = null, string? type = null, string? currentUserId = null, string? role = null);
        Task<List<FollowUp>> GetUpcomingAsync(string? currentUserId = null, string? role = null);
        Task<List<FollowUp>> GetOverdueAsync(string? currentUserId = null, string? role = null);
        Task<FollowUp?> GetByIdAsync(int id);
        Task<(bool Success, string? Error, FollowUp? FollowUp)> CreateAsync(FollowUp followUp, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error)> UpdateStatusAsync(int id, string status, string? remarks, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error)> RescheduleAsync(int id, DateTime newDate, string? remarks, string currentUserId, string? currentUserName, string? ipAddress);
        Task<(bool Success, string? Error)> DeleteAsync(int id, string currentUserId, string? currentUserName, string? ipAddress);
    }
}
