using System.ComponentModel.DataAnnotations;

namespace UserManagement.Models
{
    public class UpdateUserModel
    {
        [Required]
        public int? Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Required]
        [MinLength(8)]
        public string Password { get; set; }
    }
}