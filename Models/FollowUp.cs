using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class FollowUp
    {
        [Key]
        public int FollowUpId { get; set; }

        [Required]
        [StringLength(30)]
        [Display(Name = "Related Type")]
        public string RelatedType { get; set; } = "Customer"; // Customer, Lead, Opportunity

        [Display(Name = "Related Record ID")]
        public int RelatedId { get; set; }

        public int? CustomerId { get; set; }
        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }

        public int? LeadId { get; set; }
        [ForeignKey(nameof(LeadId))]
        public virtual Lead? Lead { get; set; }

        [Required(ErrorMessage = "Follow-up Date is required.")]
        [Display(Name = "Follow-Up Date & Time")]
        public DateTime FollowUpDate { get; set; }

        [Required(ErrorMessage = "Follow-up Type is required.")]
        [StringLength(30)]
        [Display(Name = "Activity Type")]
        public string FollowUpType { get; set; } = "Call"; // Call, Meeting, Email, Demo

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200, ErrorMessage = "Subject cannot exceed 200 characters.")]
        public string Subject { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Remarks { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Planned"; // Planned, Completed, Missed, Cancelled

        [StringLength(450)]
        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [ForeignKey(nameof(AssignedTo))]
        public virtual ApplicationUser? AssignedUser { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedDate { get; set; }
    }
}
