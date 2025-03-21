using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Radzen;
using NuclearEvaluation.Server.Services;
using NuclearEvaluation.Server.Models;
using NuclearEvaluation.Kernel.Models.Identity;

namespace NuclearEvaluation.Server.Pages
{
    public partial class EditApplicationUser
    {
        [Inject]
        protected IJSRuntime JSRuntime { get; set; }

        [Inject]
        protected NavigationManager NavigationManager { get; set; }

        [Inject]
        protected DialogService DialogService { get; set; }

        [Inject]
        protected TooltipService TooltipService { get; set; }

        [Inject]
        protected ContextMenuService ContextMenuService { get; set; }

        [Inject]
        protected NotificationService NotificationService { get; set; }

        protected IEnumerable<ApplicationRole> roles;
        protected ApplicationUser user;
        protected IEnumerable<string> userRoles;
        protected string error;
        protected bool errorVisible;

        [Parameter]
        public string Id { get; set; }

        [Inject]
        protected SecurityService Security { get; set; }

        protected override async Task OnInitializedAsync()
        {
            user = await Security.GetUserById($"{Id}");

            userRoles = user.Roles.Select(role => role.Id);

            roles = await Security.GetRoles();
        }

        protected async Task FormSubmit(ApplicationUser user)
        {
            try
            {
                user.Roles = roles.Where(role => userRoles.Contains(role.Id)).ToList();
                await Security.UpdateUser($"{Id}", user);
                DialogService.Close(null);
            }
            catch (Exception ex)
            {
                errorVisible = true;
                error = ex.Message;
            }
        }

        protected async Task CancelClick()
        {
            DialogService.Close(null);
        }
    }
}