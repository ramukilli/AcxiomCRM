using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services
{
    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(
            string? userId,
            string? userName,
            string action,
            string entityName,
            string? recordId,
            object? oldValue = null,
            object? newValue = null,
            string? details = null,
            string? ipAddress = null,
            string result = "Success")
        {
            try
            {
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    UserName = userName ?? "System",
                    Action = action,
                    EntityName = entityName,
                    RecordId = recordId,
                    OldValue = oldValue != null ? (oldValue is string strOld ? strOld : JsonSerializer.Serialize(oldValue)) : null,
                    NewValue = newValue != null ? (newValue is string strNew ? strNew : JsonSerializer.Serialize(newValue)) : null,
                    Details = details,
                    IpAddress = ipAddress ?? "127.0.0.1",
                    Result = result,
                    CreatedDate = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Never let audit log failures bring down the main user request
                Console.WriteLine($"[Audit Error] Failed to write audit record: {ex.Message}");
            }
        }

        public async Task<List<AuditLog>> GetLogsAsync(
            string? userId = null,
            string? module = null,
            string? action = null,
            DateTime? fromDate = null,
            DateTime? toDate = null)
        {
            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(a => a.UserId == userId || (a.UserName != null && a.UserName.Contains(userId)));
            }

            if (!string.IsNullOrWhiteSpace(module))
            {
                query = query.Where(a => a.EntityName.ToLower() == module.ToLower());
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action.ToLower() == action.ToLower());
            }

            if (fromDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(a => a.CreatedDate <= toDate.Value);
            }

            return await query.OrderByDescending(a => a.CreatedDate).Take(300).ToListAsync();
        }
    }
}
