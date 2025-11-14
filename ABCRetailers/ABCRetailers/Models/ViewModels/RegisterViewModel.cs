using System.ComponentModel.DataAnnotations;

namespace ABCRetailers.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string Username { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; } = "Customer";

        [Display(Name = "First name")]
        public string? Name { get; set; }

        [Display(Name = "Last name")]
        public string? Surname { get; set; }

        [EmailAddress]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Shipping Address")]
        public string? ShippingAddress { get; set; }
    }
}


