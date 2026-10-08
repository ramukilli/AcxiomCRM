using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public class FollowUpService : IFollowUpService
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuditService _auditService;

        public FollowUpService(ApplicationDbContext context, IAuditService auditService)
        {
            _context = context;
            _auditService = auditService;
        }

        public async Task<List<FollowUp>> GetAllAsync(string? status = null, string? type = null, string? currentUserId = null, string? role = null)
        {
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedUser)
                .AsQueryable();

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(f => f.AssignedTo == currentUserId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(f => f.Status.ToLower() == status.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(f => f.FollowUpType.ToLower() == type.ToLower());
            }

            return await query.OrderBy(f => f.FollowUpDate).ToListAsync();
        }

        public async Task<List<FollowUp>> GetUpcomingAsync(string? currentUserId = null, string? role = null)
        {
            var today = DateTime.UtcNow.Date;
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedUser)
                .Where(f => f.Status == "Planned" && f.FollowUpDate >= today);

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(f => f.AssignedTo == currentUserId);
            }

            return await query.OrderBy(f => f.FollowUpDate).Take(10).ToListAsync();
        }

        public async Task<List<FollowUp>> GetOverdueAsync(string? currentUserId = null, string? role = null)
        {
            var now = DateTime.UtcNow;
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedUser)
                .Where(f => f.Status == "Planned" && f.FollowUpDate < now);

            if (role == "SalesExecutive" && !string.IsNullOrEmpty(currentUserId))
            {
                query = query.Where(f => f.AssignedTo == currentUserId);
            }

            return await query.OrderBy(f => f.FollowUpDate).ToListAsync();
        }

        public async Task<FollowUp?> GetByIdAsync(int id)
        {
            return await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.AssignedUser)
                .FirstOrDefaultAsync(f => f.FollowUpId == id);
        }

        public async Task<(bool Success, string? Error, FollowUp? FollowUp)> CreateAsync(
            FollowUp followUp,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            // Business Validation: Follow-up date cannot be earlier than today
            if (followUp.Status == "Planned" && followUp.FollowUpDate.Date < DateTime.UtcNow.Date)
            {
                return (false, "Follow-up date cannot be earlier than today.", null);
            }

            followUp.CreatedDate = DateTime.UtcNow;
            if (string.IsNullOrWhiteSpace(followUp.AssignedTo))
            {
                followUp.AssignedTo = currentUserId;
            }

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Create",
                "FollowUp",
                followUp.FollowUpId.ToString(),
                oldValue: null,
                newValue: new { followUp.FollowUpId, followUp.Subject, followUp.FollowUpDate, followUp.Status, followUp.FollowUpType },
                details: $"Follow-up '{followUp.Subject}' scheduled for {followUp.FollowUpDate:yyyy-MM-dd HH:mm}.",
                ipAddress: ipAddress);

            return (true, null, followUp);
        }

        public async Task<(bool Success, string? Error)> UpdateStatusAsync(
            int id,
            string status,
            string? remarks,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var existing = await _context.FollowUps.FindAsync(id);
            if (existing == null)
            {
                return (false, "Follow-up not found.");
            }

            var oldStatus = existing.Status;
            existing.Status = status;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                existing.Remarks = (existing.Remarks != null ? existing.Remarks + " | " : "") + remarks;
            }

            if (status == "Completed")
            {
                existing.CompletedDate = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Status Change",
                "FollowUp",
                existing.FollowUpId.ToString(),
                oldValue: new { existing.FollowUpId, Status = oldStatus },
                newValue: new { existing.FollowUpId, Status = status, existing.CompletedDate },
                details: $"Follow-up status changed from '{oldStatus}' to '{status}'.",
                ipAddress: ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> RescheduleAsync(
            int id,
            DateTime newDate,
            string? remarks,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var existing = await _context.FollowUps.FindAsync(id);
            if (existing == null)
            {
                return (false, "Follow-up not found.");
            }

            if (newDate.Date < DateTime.UtcNow.Date)
            {
                return (false, "Follow-up date cannot be earlier than today.");
            }

            var oldDate = existing.FollowUpDate;
            existing.FollowUpDate = newDate;
            existing.Status = "Planned";
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                existing.Remarks = (existing.Remarks != null ? existing.Remarks + " | " : "") + $"Rescheduled: {remarks}";
            }

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Reschedule",
                "FollowUp",
                existing.FollowUpId.ToString(),
                oldValue: new { FollowUpDate = oldDate },
                newValue: new { FollowUpDate = newDate },
                details: $"Follow-up rescheduled from {oldDate:yyyy-MM-dd HH:mm} to {newDate:yyyy-MM-dd HH:mm}.",
                ipAddress: ipAddress);

            return (true, null);
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(
            int id,
            string currentUserId,
            string? currentUserName,
            string? ipAddress)
        {
            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null)
            {
                return (false, "Follow-up not found.");
            }

            _context.FollowUps.Remove(followUp);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                currentUserId,
                currentUserName,
                "Delete",
                "FollowUp",
                id.ToString(),
                oldValue: new { followUp.FollowUpId, followUp.Subject },
                newValue: null,
                details: $"Follow-up '{followUp.Subject}' deleted.",
                ipAddress: ipAddress);

            return (true, null);
        }
    }
}
