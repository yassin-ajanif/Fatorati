using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GestionCommerciale.Modules.Livraison.Views;

public partial class BLListView : UserControl
{
    public BLListView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is ViewModels.BLListViewModel vm)
            vm.LoadCommand.Execute(null);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
