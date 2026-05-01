using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace GestionCommerciale.Modules.Devis.Views;

public partial class DevisListView : UserControl
{
    public DevisListView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (DataContext is ViewModels.DevisListViewModel vm)
            vm.LoadCommand.Execute(null);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
