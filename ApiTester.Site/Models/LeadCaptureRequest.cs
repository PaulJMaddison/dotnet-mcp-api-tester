using System.ComponentModel.DataAnnotations;

namespace ApiTester.Site.Models;

public sealed class LeadCaptureRequest
{
    [Required(ErrorMessage = "First name is required.")]
    [StringLength(80)]
    public string? FirstName { get; set; }

    [Required(ErrorMessage = "Last name is required.")]
    [StringLength(80)]
    public string? LastName { get; set; }

    [Required(ErrorMessage = "Work email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid work email.")]
    public string? Email { get; set; }

    [StringLength(120)]
    public string? Company { get; set; }

    [Required(ErrorMessage = "Tell us what you want to achieve.")]
    [StringLength(500)]
    public string? Message { get; set; }

    public string? Website { get; set; }
}
