using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace PaperNotes;

/// <summary>Custom caption bar using WindowChrome (replaces the default OS title chrome).</summary>
public static class TitleBar
{
    public const double Height = 40;
    const string BrandTag = "TitleBarBrand";
    const string MinTag = "TitleBarMin";
    const string MaxTag = "TitleBarMax";
    const string CloseTag = "TitleBarClose";

    public static void Attach(Window window, string title, bool showMaximize = true)
    {
        window.WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = Height,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        });
        var chrome = Build(window, title, showMaximize);
        if (window.Content is FrameworkElement body)
        {
            window.Content = null;
            var root = new DockPanel { LastChildFill = true, Background = window.Background };
            DockPanel.SetDock(chrome, Dock.Top);
            root.Children.Add(chrome);
            root.Children.Add(body);
            window.Content = root;
        }
        else
        {
            window.Content = chrome;
        }
    }

    public static void RefreshBrand(Window window, string? title = null)
    {
        string text = title ?? Loc.BrandName;
        if (FindTagged<TextBlock>(window, BrandTag) is { } brand)
            brand.Text = text;
        if (FindTagged<Button>(window, MinTag) is { } min)
            SetCaptionName(min, Loc.T("Window.Minimize"));
        if (FindTagged<Button>(window, CloseTag) is { } close)
            SetCaptionName(close, Loc.T("Window.Close"));
        if (FindTagged<Button>(window, MaxTag) is { } max)
            UpdateMaximizeGlyph(max, window);
    }

    public static Border Build(Window window, string title, bool showMaximize)
    {
        var bar = new Border
        {
            Height = Height,
            Background = window.Background ?? Ui.Brush("#FAF9F6"),
            BorderBrush = Ui.Brush("#E4E3DB"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            SnapsToDevicePixels = true
        };
        var grid = new Grid();
        bar.Child = grid;

        var brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            IsHitTestVisible = false
        };
        try
        {
            brand.Children.Add(new Image
            {
                Source = AppIcon.ForWindow,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 8, 0),
                SnapsToDevicePixels = true
            });
        }
        catch
        {
            // Missing icon asset: show brand text only.
        }
        brand.Children.Add(new TextBlock
        {
            Text = title,
            Tag = BrandTag,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
            Foreground = Ui.Brush("#35372F")
        });
        grid.Children.Add(brand);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        WindowChrome.SetIsHitTestVisibleInChrome(buttons, true);

        var min = CaptionButton("─", Loc.T("Window.Minimize"), false);
        min.Tag = MinTag;
        min.Click += (_, _) => window.WindowState = WindowState.Minimized;
        buttons.Children.Add(min);

        Button? max = null;
        if (showMaximize)
        {
            max = CaptionButton("□", Loc.T("Window.Maximize"), false);
            max.Tag = MaxTag;
            max.Click += (_, _) => ToggleMaximize(window);
            buttons.Children.Add(max);
            window.StateChanged += (_, _) => UpdateMaximizeGlyph(max, window);
            UpdateMaximizeGlyph(max, window);
        }

        var close = CaptionButton("×", Loc.T("Window.Close"), true);
        close.Tag = CloseTag;
        close.Click += (_, _) => window.Close();
        buttons.Children.Add(close);
        grid.Children.Add(buttons);
        return bar;
    }

    static void ToggleMaximize(Window window) =>
        window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    static void UpdateMaximizeGlyph(Button button, Window window)
    {
        bool maximized = window.WindowState == WindowState.Maximized;
        button.Content = maximized ? "❐" : "□";
        SetCaptionName(button, maximized ? Loc.T("Window.Restore") : Loc.T("Window.Maximize"));
    }

    static void SetCaptionName(Button button, string name)
    {
        button.ToolTip = name;
        System.Windows.Automation.AutomationProperties.SetName(button, name);
    }

    static T? FindTagged<T>(DependencyObject root, string tag) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match && Equals(match.Tag, tag)) return match;
            if (FindTagged<T>(child, tag) is { } nested) return nested;
        }
        return null;
    }

    static Button CaptionButton(string glyph, string name, bool isClose)
    {
        var button = new Button
        {
            Content = glyph,
            Width = 46,
            Height = Height,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Ui.Brush("#49483F"),
            FontSize = isClose ? 16 : 12,
            FontFamily = new FontFamily("Segoe UI"),
            Cursor = Cursors.Hand,
            Focusable = false,
            ToolTip = name
        };
        System.Windows.Automation.AutomationProperties.SetName(button, name);
        var surface = new FrameworkElementFactory(typeof(Border));
        surface.Name = "Surface";
        surface.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        surface.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = surface };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, isClose ? Ui.Brush("#E81123") : Ui.Brush("#E4E5DA"), "Surface"));
        hover.Setters.Add(new Setter(Control.ForegroundProperty, isClose ? Brushes.White : Ui.Brush("#35372F")));
        var press = new Trigger { Property = Button.IsPressedProperty, Value = true };
        press.Setters.Add(new Setter(Border.BackgroundProperty, isClose ? Ui.Brush("#C50F1F") : Ui.Brush("#D5D6CB"), "Surface"));
        template.Triggers.Add(hover);
        template.Triggers.Add(press);
        button.Template = template;
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        return button;
    }
}
