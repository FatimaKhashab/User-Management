using System.ComponentModel.DataAnnotations;

namespace UserManagement.Models;

public class UserLoginModel
{  
    [Required]
    [EmailAddress]
    public string Email { get; set; }

    [Required]
    [MinLength(8)]
    public string Password { get; set; }
}