using System.IO;
using System.Text.Json;
using System.Windows;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using STEX.Inv.Desktop.Core.Interfaces;
using STEX.Inv.Desktop.Core.Models;
using ThemeMode = STEX.Inv.Desktop.Core.Models.ThemeMode;

namespace STEX.Inv.Desktop.UI.Services;

public sealed class ThemeService : IThemeService
{
    private const string LightThemeUri =
        "pack://application:,,,/STEX.Inv.Desktop.UI;component/Themes/LightTheme.xaml";

    private const string DarkThemeUri =
        "pack://application:,,,/STEX.Inv.Desktop.UI;component/Themes/DarkTheme.xaml";

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "STEX Inventario",
        "theme.json");

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public ThemeMode Current { get; private set; } = ThemeMode.System;

    public async Task InitializeAsync()
    {
        Current = await LoadAsync();
        Apply(Current == ThemeMode.System ? GetSystemTheme() : Current);
    }

    public async Task SetThemeAsync(ThemeMode mode)
    {
        Current = mode;
        Apply(mode == ThemeMode.System ? GetSystemTheme() : mode);
        await SaveAsync(mode);
    }

    private static void Apply(ThemeMode effectiveTheme)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (!app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(() => Apply(effectiveTheme));
            return;
        }

        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        theme.SetBaseTheme(effectiveTheme == ThemeMode.Dark ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(theme);

        var dictionaries = app.Resources.MergedDictionaries;
        var current = dictionaries.FirstOrDefault(d => d.Contains("AppBackgroundBrush"));
        var next = new ResourceDictionary
        {
            Source = new Uri(effectiveTheme == ThemeMode.Dark ? DarkThemeUri : LightThemeUri)
        };

        if (current is not null)
        {
            dictionaries.Remove(current);
        }

        dictionaries.Add(next);
    }

    private static ThemeMode GetSystemTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);

            return value is int lightEnabled && lightEnabled == 0
                ? ThemeMode.Dark
                : ThemeMode.Light;
        }
        catch
        {
            return ThemeMode.Light;
        }
    }

    private static async Task<ThemeMode> LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return ThemeMode.System;
            }

            var json = await File.ReadAllTextAsync(SettingsPath);
            var settings = JsonSerializer.Deserialize<ThemeSettings>(json);

            return Enum.TryParse<ThemeMode>(settings?.Mode, true, out var mode)
                ? mode
                : ThemeMode.System;
        }
        catch
        {
            return ThemeMode.System;
        }
    }

    private async Task SaveAsync(ThemeMode mode)
    {
        await _saveLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(new ThemeSettings { Mode = mode.ToString() });
            await File.WriteAllTextAsync(SettingsPath, json);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private sealed class ThemeSettings
    {
        public string? Mode { get; set; }
    }
}
