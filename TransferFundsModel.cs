using System.ComponentModel.DataAnnotations;

namespace UserManagement.Models;

public class TransferFundsModel
{
    [Required]
    public int? ReceiverId { get; set; }

    [Required]
    public decimal? Amount { get; set; }
}