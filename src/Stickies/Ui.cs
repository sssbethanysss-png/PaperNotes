using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PaperNotes;

public static class Ui
{
    public static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    public static Button Button(string text, string tooltip, RoutedEventHandler action)
    {
        var button = new Button { Content = text, ToolTip = tooltip, Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(2), Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Foreground = Brush("#49483F"), Cursor = System.Windows.Input.Cursors.Hand, MinHeight = 28 };
        button.Click += action;
        System.Windows.Automation.AutomationProperties.SetName(button, tooltip);
        return button;
    }
}
