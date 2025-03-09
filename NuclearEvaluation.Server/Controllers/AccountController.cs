using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NuclearEvaluation.Server.Models;
using NuclearEvaluation.Server.Models.Identity;

namespace NuclearEvaluation.Server.Controllers;

[Route("Account/[action]")]
public partial class AccountController : Controller
{
    readonly SignInManager<ApplicationUser> _signInManager;
    readonly UserManager<ApplicationUser> _userManager;
    readonly RoleManager<ApplicationRole> _roleManager;
    readonly IWebHostEnvironment _env;
    readonly IConfiguration _configuration;

    public AccountController(IWebHostEnvironment env, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager, IConfiguration configuration)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _env = env;
        _configuration = configuration;
    }

    IActionResult RedirectWithError(string error, string redirectUrl = null)
    {
        if (!string.IsNullOrEmpty(redirectUrl))
        {
            return Redirect($"~/Login?error={error}&redirectUrl={Uri.EscapeDataString(redirectUrl.Replace("~", ""))}");
        }
        else
        {
            return Redirect($"~/Login?error={error}");
        }
    }

    [HttpGet]
    public async Task<IActionResult> Login(string returnUrl)
    {
        if (returnUrl != "/" && !string.IsNullOrEmpty(returnUrl))
        {
            return Redirect($"~/Login?redirectUrl={Uri.EscapeDataString(returnUrl)}");
        }

        return Redirect("~/Login");
    }

    [HttpPost]
    public async Task<IActionResult> Login(string userName, string password, string redirectUrl)
    {
        redirectUrl = string.IsNullOrEmpty(redirectUrl) ? "~/" : redirectUrl.StartsWith("/") ? redirectUrl : $"~/{redirectUrl}";

        if (_env.IsDevelopment() && userName == "admin" && password == "admin")
        {
            List<Claim> claims =
            [
                new Claim(ClaimTypes.Name, "admin"),
                new Claim(ClaimTypes.Email, "admin"),
            ];

            _roleManager.Roles.ToList().ForEach(r => claims.Add(new Claim(ClaimTypes.Role, r.Name)));
            await _signInManager.SignInWithClaimsAsync(new ApplicationUser { UserName = userName, Email = userName }, isPersistent: false, claims);

            return Redirect(redirectUrl);
        }

        if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
        {

            ApplicationUser? user = await _userManager.FindByNameAsync(userName);

            if (user == null)
            {
                return RedirectWithError("Invalid user or password", redirectUrl);
            }

            if (!user.EmailConfirmed)
            {
                //TODO enable back once email provider is resolved
                //return RedirectWithError("User email not confirmed", redirectUrl);
            }

            bool isTenantsAdmin = userName == "tenantsadmin";

            //TODO enable back once email provider is resolved
            //bool isTwoFactor = await _userManager.GetTwoFactorEnabledAsync(user);
            bool isTwoFactor = false;
            if (!isTwoFactor && !isTenantsAdmin)
            {
                await _userManager.SetTwoFactorEnabledAsync(user, true);
            }
            Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(userName, password, false, false);

            //TODO enable back once email provider is resolved
//            if (result.RequiresTwoFactor && !isTenantsAdmin)
//            {
//                string code = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");

//                string text = $@"Hi, <br /> <br />
//We received your request for a single-use code to use with your Nuclear Evaluation account. <br /> <br />
//Your single-use code is: {code} <br /> <br />
//If you didn't request this code, you can safely ignore this email. Someone else might have typed your email address by mistake.";

//                await SendEmailAsync(user.Email, "Your single-use code", text);

//                return Redirect($"~/SecurityCode?email={Uri.EscapeDataString(user.Email)}");
//            }
            if (result.Succeeded)
            {
                return Redirect(redirectUrl);
            }
        }

        return RedirectWithError("Invalid user or password", redirectUrl);
    }

    [HttpPost]
    public async Task<IActionResult> VerifySecurityCode(string code)
    {
        Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.TwoFactorSignInAsync("Email", code, false, false);

        if (!result.Succeeded)
        {
            return RedirectWithError("Invalid security code");
        }

        return Redirect("~/");
    }
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword)
    {
        if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword))
        {
            return BadRequest("Invalid password");
        }

        string? id = User.FindFirstValue(ClaimTypes.NameIdentifier);

        ApplicationUser? user = await _userManager.FindByIdAsync(id);
        IdentityResult result = await _userManager.ChangePasswordAsync(user, oldPassword, newPassword);

        if (result.Succeeded)
        {
            return Ok();
        }

        string message = string.Join(", ", result.Errors.Select(error => error.Description));

        return BadRequest(message);
    }

    [HttpPost]
    public ApplicationAuthenticationState CurrentUser()
    {
        return new ApplicationAuthenticationState
        {
            IsAuthenticated = User.Identity.IsAuthenticated,
            Name = User.Identity.Name,
            Claims = User.Claims.Select(c => new ApplicationClaim { Type = c.Type, Value = c.Value }),
        };
    }

    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return Redirect("~/");
    }

    [HttpPost]
    public async Task<IActionResult> Register(string userName, string password)
    {
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            return BadRequest("Invalid user name or password.");
        }

        ApplicationUser user = new ApplicationUser { UserName = userName, Email = userName };
        IdentityResult result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded)
        {
            return Ok();
            try
            {
                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code }, protocol: Request.Scheme);

                var text = $@"Hi, <br /> <br />
We received your registration request for Nuclear Evaluation. <br /> <br />
To confirm your registration please click the following link: <a href=""{callbackUrl}"">confirm your registration</a> <br /> <br />
If you didn't request this registration, you can safely ignore this email. Someone else might have typed your email address by mistake.";

                await SendEmailAsync(user.Email, "Confirm your registration", text);


                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        var message = string.Join(", ", result.Errors.Select(error => error.Description));

        return BadRequest(message);
    }

    public async Task<IActionResult> ConfirmEmail(string userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId);

        var result = await _userManager.ConfirmEmailAsync(user, code);

        if (result.Succeeded)
        {
            return Redirect("~/Login?info=Your registration has been confirmed");
        }

        return RedirectWithError("Invalid user or confirmation code");
    }

    public async Task<IActionResult> ResetPassword(string userName)
    {
        var user = await _userManager.FindByNameAsync(userName);

        if (user == null)
        {
            return BadRequest("Invalid user name.");
        }

        try
        {
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);

            var callbackUrl = Url.Action("ConfirmPasswordReset", "Account", new { userId = user.Id, code }, protocol: Request.Scheme);

            var body = string.Format(@"<a href=""{0}"">{1}</a>", callbackUrl, "Please confirm your password reset.");

            await SendEmailAsync(user.Email, "Confirm your password reset", body);

            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public async Task<IActionResult> ConfirmPasswordReset(string userId, string code)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Redirect("~/Login?error=Invalid user");
        }

        var password = GenerateRandomPassword();

        var result = await _userManager.ResetPasswordAsync(user, code, password);

        if (result.Succeeded)
        {
            await SendEmailAsync(user.Email, "New password", $"<p>Your new password is: {password}</p><p>Please change it after login.</p>");

            return Redirect("~/Login?info=Password reset successful. You will receive an email with your new password.");
        }

        return Redirect("~/Login?error=Invalid user or confirmation code");
    }

    static string GenerateRandomPassword()
    {
        var options = new PasswordOptions
        {
            RequiredLength = 8,
            RequiredUniqueChars = 4,
            RequireDigit = true,
            RequireLowercase = true,
            RequireNonAlphanumeric = true,
            RequireUppercase = true
        };

        var randomChars = new[] {
            "ABCDEFGHJKLMNOPQRSTUVWXYZ",
            "abcdefghijkmnopqrstuvwxyz",
            "0123456789",
            "!@$?_-"
        };

        var rand = new Random(Environment.TickCount);
        var chars = new List<char>();

        if (options.RequireUppercase)
        {
            chars.Insert(rand.Next(0, chars.Count), randomChars[0][rand.Next(0, randomChars[0].Length)]);
        }

        if (options.RequireLowercase)
        {
            chars.Insert(rand.Next(0, chars.Count), randomChars[1][rand.Next(0, randomChars[1].Length)]);
        }

        if (options.RequireDigit)
        {
            chars.Insert(rand.Next(0, chars.Count), randomChars[2][rand.Next(0, randomChars[2].Length)]);
        }

        if (options.RequireNonAlphanumeric)
        {
            chars.Insert(rand.Next(0, chars.Count), randomChars[3][rand.Next(0, randomChars[3].Length)]);
        }

        for (int i = chars.Count; i < options.RequiredLength || chars.Distinct().Count() < options.RequiredUniqueChars; i++)
        {
            string rcs = randomChars[rand.Next(0, randomChars.Length)];
            chars.Insert(rand.Next(0, chars.Count), rcs[rand.Next(0, rcs.Length)]);
        }

        return new string(chars.ToArray());
    }

    async Task SendEmailAsync(string to, string subject, string body)
    {

        System.Net.Mail.MailMessage mailMessage = new System.Net.Mail.MailMessage();
        mailMessage.From = new System.Net.Mail.MailAddress(_configuration.GetValue<string>("Smtp:User"));
        mailMessage.Body = body;
        mailMessage.Subject = subject;
        mailMessage.BodyEncoding = System.Text.Encoding.UTF8;
        mailMessage.SubjectEncoding = System.Text.Encoding.UTF8;
        mailMessage.IsBodyHtml = true;
        mailMessage.To.Add(to);

        System.Net.Mail.SmtpClient client = new System.Net.Mail.SmtpClient(_configuration.GetValue<string>("Smtp:Host"))
        {
            UseDefaultCredentials = false,
            EnableSsl = _configuration.GetValue<bool>("Smtp:Ssl"),
            Port = _configuration.GetValue<int>("Smtp:Port"),
            Credentials = new System.Net.NetworkCredential(_configuration.GetValue<string>("Smtp:User"), _configuration.GetValue<string>("Smtp:Password"))
        };

        await client.SendMailAsync(mailMessage);
    }
}
