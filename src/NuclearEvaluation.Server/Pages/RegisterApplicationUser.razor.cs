using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Radzen;
using Radzen.Blazor;
using NuclearEvaluation.Server.Services;
using NuclearEvaluation.Kernel.Models.Identity;

namespace NuclearEvaluation.Server.Pages
{
    public partial class RegisterApplicationUser
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

        protected ApplicationUser user;
        protected bool isBusy;
        protected bool errorVisible;
        protected string error;

        [Inject]
        protected SecurityService Security { get; set; }

        protected override async Task OnInitializedAsync()
        {
            user = new ApplicationUser
            {
                TwoFactorEnabled = false,
            };
        }

        protected async Task FormSubmit()
        {
            try
            {
                isBusy = true;

                if (user.Password != user.ConfirmPassword)
                {
                    throw new Exception($"Passwords do not match");
                }

                await Security.Register(user.Email, user.Password);

                DialogService.Close(true);
            }
            catch (Exception ex)
            {
                errorVisible = true;
                error = ex.Message;
            }

            isBusy = false;
        }

        protected async Task CancelClick()
        {
            DialogService.Close(false);
        }
    }
}