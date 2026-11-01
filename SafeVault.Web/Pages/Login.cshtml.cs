using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using SafeVault.Web.Data;

namespace SafeVault.Web.Pages;

[EnableRateLimiting("login")]
public sealed class LoginModel(UserRepository userRepository) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!InputValidator.IsValidPassword(Input.Password))
        {
            ModelState.AddModelError("Input.Password", "Invalid username or password");
            return Page();
        }

        var user = userRepository.GetByUsername(Input.Username);
        if (user?.PasswordHash is not { Length: > 0 } passwordHash ||
            !BCrypt.Net.BCrypt.Verify(Input.Password, passwordHash))
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password");
            return Page();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Url.IsLocalUrl(ReturnUrl)
            ? LocalRedirect(ReturnUrl!)
            : RedirectToPage("/Index");
    }

    public sealed class LoginInput
    {
        [Required]
        [StringLength(30)]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(72)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;
    }
}
