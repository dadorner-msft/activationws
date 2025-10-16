using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ActivationWs.Models
{
    public class ActivationRecord {
        public int Id { get; set; }
        
        [Required] 
        public int MachineId { get; set; }

        [Required]
        public Machine Machine { get; set; } = null!;

        [Required]
        public string ExtendedProductID { get; set; } = string.Empty;
        
        [Required]
        public string InstallationID { get; set; } = string.Empty;
        
        [Required]
        public string ConfirmationID { get; set; } = string.Empty;
        
        [Required]
        public DateTime LicenseAcquisitionDate { get; set; }
    }
}