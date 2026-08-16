namespace lotus_blue.Models.ViewModel
{
    // Store data to send for sbs kargo 
    public class StoreDataViewModel
    {
        public int Id { get; set; }
        public string? LogoUrl { get; set; }
        public string? InformationUrl { get; set; }
        public string? Name { get; set; }
        public string? TaxRegistrationNumber { get; set; }
        public string? Address { get; set; }
        public string? IdNumber { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Specialty { get; set; }
        public string? Website { get; set; }
        public string? Notes { get; set; }
        public string? SelectedCountry { get; set; }
        public string? City { get; set; }

        // Add any other properties that are included in RepresentativesViewModel
    }

}
