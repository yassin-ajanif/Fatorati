using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using GestionCommerciale.Modules.Pos.ViewModels;

namespace GestionCommerciale.Modules.Pos.Views;

public partial class PosView : UserControl
{
    private InputElement? _focusedInput;
    private NumericUpDown? _numericTarget;
    private bool _keyboardEnabled;
    private bool _isNumericTarget;
    private string _numBuffer = string.Empty;

    public PosView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnOverlayPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        HideKeyboard();
    }

    private AutoCompleteBox? _clientBox;

    private void OnClientDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        _isNumericTarget = false;
        _numBuffer = string.Empty;
        _focusedInput = sender as InputElement;
        _clientBox = this.FindControl<AutoCompleteBox>("ClientBox");
        _clientBox?.Focus();
        _numericTarget = null;
        ShowKeyboard();
    }

    private void OnSearchDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        _isNumericTarget = false;
        _numBuffer = string.Empty;
        _focusedInput = null;
        _clientBox = null;
        _numericTarget = null;
        ShowKeyboard();
    }

    private void OnNumericDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        _isNumericTarget = true;
        _numBuffer = string.Empty;
        _clientBox = null;
        _numericTarget = sender as NumericUpDown;
        if (_numericTarget == null && sender is Control c)
            _numericTarget = c.FindAncestorOfType<NumericUpDown>();
        _focusedInput = _numericTarget;
        ShowKeyboard();
    }

    private void ShowKeyboard()
    {
        if (!_keyboardEnabled) return;
        var kb = this.FindControl<Shared.Controls.VirtualKeyboard>("PosKeyboard");
        if (kb == null) return;
        kb.IsAlphaVisible = !_isNumericTarget;
        var overlay = this.FindControl<Grid>("KeyboardOverlay");
        if (overlay != null) overlay.IsVisible = true;
    }

    private void HideKeyboard()
    {
        var overlay = this.FindControl<Grid>("KeyboardOverlay");
        if (overlay != null) overlay.IsVisible = false;
        _focusedInput = null;
        _clientBox = null;
        _numericTarget = null;
    }

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
        var text = vm.SearchText?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            vm.SearchText = string.Empty;
            return;
        }

        var match = vm.SearchResults.FirstOrDefault(r =>
            !string.IsNullOrEmpty(r.CodeBarre) &&
            r.CodeBarre.Equals(text, StringComparison.OrdinalIgnoreCase));

        if (match is not null)
            vm.AddProductCommand.Execute(match);

        vm.SearchText = string.Empty;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is PosViewModel vm)
        {
            _keyboardEnabled = vm.ShowKeyboard;
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PosViewModel.ShowKeyboard) && DataContext is PosViewModel vm)
            _keyboardEnabled = vm.ShowKeyboard;
    }

    private void SetNumericValue(decimal val)
    {
        if (_numericTarget == null) return;
        _numericTarget.SetCurrentValue(NumericUpDown.ValueProperty, val);
    }

    private void OnKeyboardKeyPressed(string ch)
    {
        if (_clientBox != null)
        {
            _clientBox.Text += ch;
            _clientBox.IsDropDownOpen = true;
        }
        else if (_isNumericTarget && _numericTarget != null)
        {
            if (ch == "." && _numBuffer.Contains("."))
                return;
            if (_numBuffer.Length < 16)
                _numBuffer += ch;
            if (decimal.TryParse(_numBuffer, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                SetNumericValue(val);
        }
        else if (!_isNumericTarget && DataContext is PosViewModel vm)
        {
            vm.SearchText += ch;
        }
    }

    private void OnKeyboardBackspace()
    {
        if (_clientBox != null)
        {
            var t = _clientBox.Text ?? string.Empty;
            if (t.Length > 0)
                _clientBox.Text = t[..^1];
        }
        else if (_isNumericTarget && _numericTarget != null)
        {
            if (_numBuffer.Length > 0)
                _numBuffer = _numBuffer[..^1];
            if (_numBuffer.Length == 0)
            {
                SetNumericValue(0);
                return;
            }
            if (decimal.TryParse(_numBuffer, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var val))
                SetNumericValue(val);
        }
        else if (!_isNumericTarget && DataContext is PosViewModel vm && vm.SearchText.Length > 0)
        {
            vm.SearchText = vm.SearchText[..^1];
        }
    }

    private void OnKeyboardClear()
    {
        if (_clientBox != null)
        {
            _clientBox.Text = string.Empty;
        }
        else if (_isNumericTarget && _numericTarget != null)
        {
            _numBuffer = string.Empty;
            SetNumericValue(0);
        }
        else if (!_isNumericTarget && DataContext is PosViewModel vm)
        {
            vm.SearchText = string.Empty;
        }
    }

    private void OnKeyboardClose()
    {
        HideKeyboard();
    }
}
