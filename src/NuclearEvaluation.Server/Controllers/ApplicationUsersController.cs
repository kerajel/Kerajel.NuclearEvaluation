using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.AspNetCore.OData.Results;
using NuclearEvaluation.Kernel.Models.Identity;
using NuclearEvaluation.Kernel.Data.Context;

namespace NuclearEvaluation.Server.Controllers;

[Authorize]
[Route("odata/Identity/ApplicationUsers")]
public partial class ApplicationUsersController : ODataController
{
    readonly NuclearEvaluationServerDbContext _context;
    readonly UserManager<ApplicationUser> _userManager;


    public ApplicationUsersController(
        NuclearEvaluationServerDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    partial void OnUsersRead(ref IQueryable<ApplicationUser> users);

    [EnableQuery]
    [HttpGet]
    public IEnumerable<ApplicationUser> Get()
    {
        var users = _userManager.Users;
        OnUsersRead(ref users);

        return users;
    }

    [EnableQuery]
    [HttpGet("{Id}")]
    public SingleResult<ApplicationUser> GetApplicationUser(string key)
    {
        var user = _context.Users.Where(i => i.Id == key);

        return SingleResult.Create(user);
    }

    partial void OnUserDeleted(ApplicationUser user);

    [HttpDelete("{Id}")]
    public async Task<IActionResult> Delete(string key)
    {
        var user = await _userManager.FindByIdAsync(key);

        if (user == null)
        {
            return NotFound();
        }

        OnUserDeleted(user);

        var result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            return IdentityError(result);
        }

        return new NoContentResult();
    }

    partial void OnUserUpdated(ApplicationUser user);

    [HttpPatch("{Id}")]
    public async Task<IActionResult> Patch(string key, [FromBody] ApplicationUser data)
    {
        var user = await _userManager.FindByIdAsync(key);

        if (user == null)
        {
            return NotFound();
        }

        OnUserUpdated(data);

        IdentityResult result = null;

        user.Roles = null;

        result = await _userManager.UpdateAsync(user);

        if (data.Roles != null)
        {
            result = await _userManager.RemoveFromRolesAsync(user, await _userManager.GetRolesAsync(user));

            if (result.Succeeded)
            {
                result = await _userManager.AddToRolesAsync(user, data.Roles.Select(r => r.Name));
            }
        }

        if (!string.IsNullOrEmpty(data.Password))
        {
            result = await _userManager.RemovePasswordAsync(user);

            if (result.Succeeded)
            {
                result = await _userManager.AddPasswordAsync(user, data.Password);
            }

            if (!result.Succeeded)
            {
                return IdentityError(result);
            }
        }

        if (result != null && !result.Succeeded)
        {
            return IdentityError(result);
        }

        return new NoContentResult();
    }

    partial void OnUserCreated(ApplicationUser user);

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ApplicationUser user)
    {
        user.UserName = user.Email;
        user.EmailConfirmed = true;
        var password = user.Password;
        var roles = user.Roles;
        user.Roles = null;
        IdentityResult result = await _userManager.CreateAsync(user, password);

        if (result.Succeeded && roles != null)
        {
            result = await _userManager.AddToRolesAsync(user, roles.Select(r => r.Name));
        }

        user.Roles = roles;

        if (result.Succeeded)
        {
            OnUserCreated(user);

            return Created($"odata/Identity/Users('{user.Id}')", user);
        }
        else
        {
            return IdentityError(result);
        }
    }

    IActionResult IdentityError(IdentityResult result)
    {
        var message = string.Join(", ", result.Errors.Select(error => error.Description));

        return BadRequest(new { error = new { message } });
    }
}