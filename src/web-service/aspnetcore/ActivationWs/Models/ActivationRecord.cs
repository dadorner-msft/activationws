namespace ActivationWs.Models
{
    public class ActivationRecord
    {
        public int Id { get; set; }
        public string Hostname { get; set; }
        public string InstallationId { get; set; }
        public string ExtendedProductId { get; set; }
        public string ConfirmationId { get; set; }
        public DateTime ActivationDate { get; set; }
        public DateTime LastRequestDate { get; set; }
        public string LicenseStatus { get; set; }

    }

}
