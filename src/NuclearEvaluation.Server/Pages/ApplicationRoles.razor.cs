using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using NuclearEvaluation.Kernel.Models.Identity;
using NuclearEvaluation.Server.Services.Security;

namespace NuclearEvaluation.Server.Pages
{
    public partial class ApplicationRoles
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
        protected RadzenDataGrid<ApplicationRole> grid0;
        protected string error;
        protected bool errorVisible;

        [Inject]
        protected SecurityService Security { get; set; }

        protected override async Task OnInitializedAsync()
        {
            roles = await Security.GetRoles();
        }

        protected async Task AddClick()
        {
            await DialogService.OpenAsync<AddApplicationRole>("Add Application Role");

            roles = await Security.GetRoles();
        }

        protected async Task DeleteClick(ApplicationRole role)
        {
            try
            {
                if (await DialogService.Confirm("Are you sure you want to delete this role?") == true)
                {
                    await Security.DeleteRole($"{role.Id}");

                    roles = await Security.GetRoles();
                }
            }
            catch (Exception ex)
            {
                errorVisible = true;
                error = ex.Message;
            }
        }
    }
}