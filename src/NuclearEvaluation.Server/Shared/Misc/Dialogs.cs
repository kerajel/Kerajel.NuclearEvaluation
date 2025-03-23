using Radzen;

namespace NuclearEvaluation.Server.Shared.Misc;

public static class Dialogs
{
    public static ConfirmOptions YesNoConfirmOptions
    {
        get
        {
            return new ConfirmOptions()
            {
                OkButtonText = "Yes",
                CancelButtonText = "No",
            };
        }
    }
}
