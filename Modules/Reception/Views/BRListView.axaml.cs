using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GestionCommerciale.Modules.Reception.Views;

public partial class BRListView : UserControl
{
    public BRListView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is ViewModels.BRListViewModel vm)
            vm.LoadCommand.Execute(null);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
