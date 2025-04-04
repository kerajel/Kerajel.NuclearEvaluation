using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NuclearEvaluation.Server.Services.Security;
using Radzen;
using Radzen.Blazor;

namespace NuclearEvaluation.Server.Shared
{
    public partial class MainLayout
    {
        [Inject]
        protected IJSRuntime JSRuntime { get; set; } = null!;

        [Inject]
        protected NavigationManager NavigationManager { get; set; } = null!;

        [Inject]
        protected DialogService DialogService { get; set; } = null!;

        [Inject]
        protected TooltipService TooltipService { get; set; } = null!;

        [Inject]
        protected ContextMenuService ContextMenuService { get; set; } = null!;

        [Inject]
        protected NotificationService NotificationService { get; set; } = null!;

        [Inject]
        protected SecurityService Security { get; set; } = null!;

        bool _sidebarExpanded = true;

        void SidebarToggleClick()
        {
            _sidebarExpanded = !_sidebarExpanded;
        }

        protected void ProfileMenuClick(RadzenProfileMenuItem args)
        {
            if (args.Value == "Logout")
            {
                Security.Logout();
            }
        }
    }
}