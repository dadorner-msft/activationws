using System.ComponentModel.DataAnnotations;

namespace ActivationWs.Models
{
    public class Machine {
        public int Id { get; set; }
        
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Hostname { get; set; } = string.Empty;

        public ICollection<ActivationRecord> ActivationRecords { get; set; } = new List<ActivationRecord>();
    }
}