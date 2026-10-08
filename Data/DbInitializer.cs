using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            await context.Database.EnsureCreatedAsync();

            // 1. Seed Roles
            string[] roles = { "Admin", "Manager", "SalesExecutive" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new ApplicationRole(role)
                    {
                        Description = $"{role} role with specific CRM privileges"
                    });
                }
            }

            // 2. Seed Users
            // Admin user
            var adminEmail = "admin@acxiom.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    Department = "Executive / IT",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Admin@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Manager user
            var managerEmail = "manager@acxiom.com";
            var managerUser = await userManager.FindByEmailAsync(managerEmail);
            if (managerUser == null)
            {
                managerUser = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    FullName = "Sarah Jenkins (Sales Manager)",
                    Department = "Sales Management",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(managerUser, "Manager@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(managerUser, "Manager");
                }
            }

            // Sales Executive user
            var salesEmail = "sales@acxiom.com";
            var salesUser = await userManager.FindByEmailAsync(salesEmail);
            if (salesUser == null)
            {
                salesUser = new ApplicationUser
                {
                    UserName = salesEmail,
                    Email = salesEmail,
                    FullName = "David Miller (Sales Rep)",
                    Department = "Direct Sales",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(salesUser, "Sales@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(salesUser, "SalesExecutive");
                }
            }

            // 3. Seed Customers
            if (!context.Customers.Any())
            {
                var customers = new[]
                {
                    new Customer
                    {
                        CustomerCode = "CUST-1001",
                        CustomerName = "Apex Global Solutions",
                        Email = "contact@apexsolutions.com",
                        Phone = "9876543210",
                        CompanyName = "Apex Solutions Inc.",
                        Address = "100 Innovation Boulevard",
                        City = "San Jose",
                        State = "CA",
                        Status = "Active",
                        CreatedDate = DateTime.UtcNow.AddMonths(-3),
                        CreatedBy = salesUser.Id
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-1002",
                        CustomerName = "Beacon Logistics Corp",
                        Email = "info@beaconlogistics.com",
                        Phone = "9876543211",
                        CompanyName = "Beacon Logistics",
                        Address = "45 Harbor View Drive",
                        City = "Seattle",
                        State = "WA",
                        Status = "Active",
                        CreatedDate = DateTime.UtcNow.AddMonths(-2),
                        CreatedBy = salesUser.Id
                    },
                    new Customer
                    {
                        CustomerCode = "CUST-1003",
                        CustomerName = "Nexus Health Network",
                        Email = "procurement@nexushealth.org",
                        Phone = "9876543212",
                        CompanyName = "Nexus Health",
                        Address = "720 Medical Center Parkway",
                        City = "Chicago",
                        State = "IL",
                        Status = "Active",
                        CreatedDate = DateTime.UtcNow.AddMonths(-1),
                        CreatedBy = adminUser.Id
                    }
                };

                context.Customers.AddRange(customers);
                await context.SaveChangesAsync();
            }

            // 4. Seed Leads
            if (!context.Leads.Any())
            {
                var leads = new[]
                {
                    new Lead
                    {
                        LeadCode = "LEAD-1001",
                        LeadName = "Michael Vance",
                        Email = "mvance@crestview.io",
                        Phone = "9876543220",
                        CompanyName = "Crestview Cloud Tech",
                        Source = "Website",
                        Status = "Qualified",
                        Priority = "High",
                        ExpectedValue = 45000m,
                        Notes = "Needs enterprise CRM cloud rollout in Q4.",
                        CreatedDate = DateTime.UtcNow.AddDays(-15),
                        AssignedTo = salesUser.Id
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-1002",
                        LeadName = "Elena Rostova",
                        Email = "elena.r@quantumai.dev",
                        Phone = "9876543221",
                        CompanyName = "Quantum AI Labs",
                        Source = "Referral",
                        Status = "Contacted",
                        Priority = "Medium",
                        ExpectedValue = 75000m,
                        Notes = "In initial demonstration phase.",
                        CreatedDate = DateTime.UtcNow.AddDays(-10),
                        AssignedTo = salesUser.Id
                    },
                    new Lead
                    {
                        LeadCode = "LEAD-1003",
                        LeadName = "Robert Chen",
                        Email = "rchen@summitretail.com",
                        Phone = "9876543222",
                        CompanyName = "Summit Retailers",
                        Source = "Partner",
                        Status = "New",
                        Priority = "Low",
                        ExpectedValue = 20000m,
                        Notes = "Inbound query regarding multi-store integrations.",
                        CreatedDate = DateTime.UtcNow.AddDays(-3),
                        AssignedTo = salesUser.Id
                    }
                };

                context.Leads.AddRange(leads);
                await context.SaveChangesAsync();
            }

            // 5. Seed Opportunities
            if (!context.Opportunities.Any())
            {
                var customer1 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1001");
                var customer2 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1002");

                var opps = new[]
                {
                    new Opportunity
                    {
                        OpportunityName = "Apex Cloud Migration Contract",
                        CustomerId = customer1?.CustomerId,
                        Amount = 65000m,
                        Stage = "Negotiation",
                        Probability = 75,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(25),
                        Status = "Open",
                        Source = "Referral",
                        Notes = "Final contract review with legal counsel.",
                        CreatedDate = DateTime.UtcNow.AddDays(-20),
                        AssignedTo = salesUser.Id
                    },
                    new Opportunity
                    {
                        OpportunityName = "Beacon Fleet Tracking Integration",
                        CustomerId = customer2?.CustomerId,
                        Amount = 42000m,
                        Stage = "Proposal",
                        Probability = 50,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(40),
                        Status = "Open",
                        Source = "Website",
                        Notes = "Proposal delivered; waiting for decision maker review.",
                        CreatedDate = DateTime.UtcNow.AddDays(-12),
                        AssignedTo = salesUser.Id
                    },
                    new Opportunity
                    {
                        OpportunityName = "Nexus Annual SaaS License",
                        CustomerId = customer1?.CustomerId,
                        Amount = 110000m,
                        Stage = "Won",
                        Probability = 100,
                        ExpectedCloseDate = DateTime.UtcNow.AddDays(-5),
                        Status = "Won",
                        Source = "Existing Business",
                        Notes = "Contract signed and approved.",
                        CreatedDate = DateTime.UtcNow.AddDays(-45),
                        AssignedTo = adminUser.Id
                    }
                };

                context.Opportunities.AddRange(opps);
                await context.SaveChangesAsync();
            }

            // 6. Seed FollowUps
            if (!context.FollowUps.Any())
            {
                var customer1 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1001");
                var lead1 = await context.Leads.FirstOrDefaultAsync(l => l.LeadCode == "LEAD-1001");

                var followUps = new[]
                {
                    new FollowUp
                    {
                        RelatedType = "Customer",
                        RelatedId = customer1?.CustomerId ?? 1,
                        CustomerId = customer1?.CustomerId,
                        FollowUpDate = DateTime.UtcNow.AddDays(2),
                        FollowUpType = "Meeting",
                        Subject = "Executive Project Review Call",
                        Remarks = "Discuss Q4 expansion strategy and contract renewal terms.",
                        Status = "Planned",
                        AssignedTo = salesUser.Id,
                        CreatedDate = DateTime.UtcNow
                    },
                    new FollowUp
                    {
                        RelatedType = "Lead",
                        RelatedId = lead1?.LeadId ?? 1,
                        LeadId = lead1?.LeadId,
                        FollowUpDate = DateTime.UtcNow.AddDays(1),
                        FollowUpType = "Call",
                        Subject = "Follow-up on product demonstration",
                        Remarks = "Address security compliance questions.",
                        Status = "Planned",
                        AssignedTo = salesUser.Id,
                        CreatedDate = DateTime.UtcNow
                    }
                };

                context.FollowUps.AddRange(followUps);
                await context.SaveChangesAsync();
            }

            // 7. Seed Activities
            if (!context.Activities.Any())
            {
                var customer1 = await context.Customers.FirstOrDefaultAsync(c => c.CustomerCode == "CUST-1001");
                var activities = new[]
                {
                    new Activity
                    {
                        ActivityType = "Call",
                        Subject = "Introductory Discovery Call",
                        Description = "Clarified system architecture and user volume.",
                        ActivityDate = DateTime.UtcNow.AddDays(-5),
                        CustomerId = customer1?.CustomerId,
                        AssignedTo = salesUser.Id,
                        Status = "Completed",
                        CreatedDate = DateTime.UtcNow.AddDays(-5)
                    },
                    new Activity
                    {
                        ActivityType = "Email",
                        Subject = "Sent Service Proposal & Pricing Grid",
                        Description = "Delivered PDF pricing matrix to client stakeholders.",
                        ActivityDate = DateTime.UtcNow.AddDays(-3),
                        CustomerId = customer1?.CustomerId,
                        AssignedTo = salesUser.Id,
                        Status = "Completed",
                        CreatedDate = DateTime.UtcNow.AddDays(-3)
                    }
                };

                context.Activities.AddRange(activities);
                await context.SaveChangesAsync();
            }

            // 8. Seed Audit Log
            if (!context.AuditLogs.Any())
            {
                context.AuditLogs.Add(new AuditLog
                {
                    UserId = adminUser.Id,
                    UserName = adminUser.UserName,
                    Action = "System Initialization",
                    EntityName = "Security",
                    RecordId = "0",
                    OldValue = null,
                    NewValue = "Database initialized with core entities and security roles.",
                    Result = "Success",
                    Details = "Baseline setup executed successfully.",
                    IpAddress = "127.0.0.1",
                    CreatedDate = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }
        }
    }
}
