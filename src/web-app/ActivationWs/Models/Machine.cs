using System.ComponentModel.DataAnnotations;

namespace ActivationWs.Models
{
    public class Machine {
        public int Id { get; set; }
        
        [Required]
        public string Hostname { get; set; }

        public ICollection<ActivationRecord> ActivationRecords { get; set; }
    }
}