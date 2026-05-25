using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using GestionCommerciale.Modules.Pos.ViewModels;

namespace GestionCommerciale.Modules.Pos.Views;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is PosViewModel vm)
            vm.SearchProductsCommand.Execute(null);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (DataContext is not PosViewModel vm) return;
        e.Handled = true;

        if (vm.SelectedProduct is not null)
            vm.AddProductCommand.Execute(vm.SelectedProduct);
        else if (vm.SearchResults.Count > 0)
            vm.AddProductCommand.Execute(vm.SearchResults[0]);

        vm.SearchText = string.Empty;
    }

}
