namespace NuclearEvaluation.Client.Shared
{
    public partial class MainLayout
    {
        bool _sidebarExpanded = true;

        void SidebarToggleClick()
        {
            _sidebarExpanded = !_sidebarExpanded;
        }
    }
}