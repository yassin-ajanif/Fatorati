using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace GestionCommerciale.Shared.Services;

public sealed class DialogService : IDialogService
{
    private static Window? GetMainWindow() =>
        Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d
            ? d.MainWindow
            : null;

    public async Task ShowInfoAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        var owner = GetMainWindow();
        var w = new Window
        {
            Title = title,
            MinWidth = 260,
            MaxWidth = 440,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 400
        });

        var ok = new Button { Content = "OK", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        ok.Click += (_, _) => w.Close();
        panel.Children.Add(ok);
        w.Content = panel;

        if (owner != null)
            await w.ShowDialog(owner);
        else
            w.Show();
    }

    public Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken = default) =>
        ShowInfoAsync(title, message, cancellationToken);

    public async Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        var owner = GetMainWindow();
        var w = new Window
        {
            Title = title,
            MinWidth = 260,
            MaxWidth = 440,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var confirmed = false;
        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 400
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };

        var no = new Button { Content = "Non" };
        no.Click += (_, _) =>
        {
            confirmed = false;
            w.Close();
        };
        var yes = new Button { Content = "Oui", IsDefault = true };
        yes.Click += (_, _) =>
        {
            confirmed = true;
            w.Close();
        };
        buttons.Children.Add(no);
        buttons.Children.Add(yes);
        panel.Children.Add(buttons);
        w.Content = panel;

        if (owner != null)
            await w.ShowDialog(owner);
        else
            w.Show();

        return confirmed;
    }

    public async Task<string?> PromptPasswordAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        var owner = GetMainWindow();
        var w = new Window
        {
            Title = title,
            MinWidth = 300,
            MaxWidth = 460,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        string? password = null;
        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 420
        });

        var input = new TextBox
        {
            PasswordChar = '*',
            MinWidth = 260
        };
        panel.Children.Add(input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var cancel = new Button { Content = "Annuler" };
        cancel.Click += (_, _) =>
        {
            password = null;
            w.Close();
        };
        var ok = new Button { Content = "Valider", IsDefault = true };
        ok.Click += (_, _) =>
        {
            password = input.Text;
            w.Close();
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        panel.Children.Add(buttons);
        w.Content = panel;

        if (owner != null)
            await w.ShowDialog(owner);
        else
            w.Show();

        return password;
    }

    public async Task<string?> PickOpenFileAsync(string title, IReadOnlyList<string> patterns, CancellationToken cancellationToken = default)
    {
        var owner = GetMainWindow();
        if (owner?.StorageProvider is not { } sp) return null;

        var filters = new List<FilePickerFileType>
        {
            new(title) { Patterns = patterns.ToList() }
        };

        var result = await sp.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveFileAsync(string title, string suggestedFileName, IReadOnlyList<string> patterns, CancellationToken cancellationToken = default)
    {
        var owner = GetMainWindow();
        if (owner?.StorageProvider is not { } sp) return null;

        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new(title) { Patterns = patterns.ToList() }
            }
        });

        return file?.TryGetLocalPath();
    }

    public async Task<bool> SavePickedFileBytesAsync(string title, string suggestedFileName, IReadOnlyList<string> patterns, byte[] content, CancellationToken cancellationToken = default)
    {
        var owner = GetMainWindow();
        if (owner?.StorageProvider is not { } sp) return false;

        var file = await sp.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new(title) { Patterns = patterns.ToList() }
            }
        });

        if (file == null) return false;

        await using var stream = await file.OpenWriteAsync();
        await stream.WriteAsync(content, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        return true;
    }
}
