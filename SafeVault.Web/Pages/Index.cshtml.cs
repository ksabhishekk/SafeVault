using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using SafeVault.Web.Data;

namespace SafeVault.Web.Pages;

public class IndexModel(UserRepository userRepository) : PageModel
{
    [BindProperty]
    public RegistrationInput Input { get; set; } = new();

    public void OnGet()
    {
    }

    public IActionResult OnPost()
    {
        if (!InputValidator.IsValidUsername(Input.Username))
        {
            ModelState.AddModelError("Input.Username", "Use 3 to 30 letters, digits, or underscores.");
        }

        if (!InputValidator.IsValidEmail(Input.Email))
        {
            ModelState.AddModelError("Input.Email", "Enter a valid email address.");
        }

        if (!InputValidator.IsValidPassword(Input.Password))
        {
            ModelState.AddModelError("Input.Password", "Use a password of 8 to 72 characters and at most 72 UTF-8 bytes.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(Input.Password);
        try
        {
            userRepository.InsertUser(Input.Username, Input.Email, passwordHash, role: "user");
        }
        catch (SqliteException exception) when (exception.SqliteExtendedErrorCode == 2067)
        {
            ModelState.AddModelError("Input.Username", "That username is already registered.");
            return Page();
        }

        TempData["StatusMessage"] = "Account created.";
        return RedirectToPage();
    }

    public sealed class RegistrationInput
    {
        [Required]
        [StringLength(30)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(254)]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(72, MinimumLength = 8)]
        [MinLength(8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}
