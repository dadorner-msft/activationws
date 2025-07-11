using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ActivationWs.Models
{
    public class ActivationRecord {
        public int Id { get; set; }
        
        [Required] 
        public int MachineId { get; set; }

        [Required]
        public Machine Machine { get; set; }

        //public string ProductName { get; set; }
        //public string ProductDescription { get; set; }
        //public string ProductKeyChannel { get; set; }
        //public string ApplicationID { get; set; }
        
        [Required]
        public string ExtendedProductID { get; set; }
        
        [Required]
        public string InstallationID { get; set; }
        
        [Required]
        public string ConfirmationID { get; set; }
        
        [Required]
        public DateTime LicenseAcquisitionDate { get; set; }
    }
}