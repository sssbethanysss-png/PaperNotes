using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace PaperNotes;

public readonly record struct CategoryDraft(string Name, string Color);

public static class CategoryDialog
{
    public static CategoryDraft? Prompt(Window owner, string title, string confirm, string name, string color, Func<string, string?> validate)
    {
        var window = new CategoryDialogWindow(title, confirm, name, color, validate) { Owner = owner };
        return window.ShowDialog() == true ? window.Result : null;
    }

    private sealed class CategoryDialogWindow : Window
    {
        private static readonly ControlTemplate SwatchTemplate = CreateSwatchTemplate();
        public CategoryDraft Result { get; private set; }
        private string selected;

        public CategoryDialogWindow(string title, string confirmText, string name, string color, Func<string, string?> validate)
        {
            Title = title; Width = 420; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
            AppIcon.Apply(this);
            FontFamily = new FontFamily("Yu Gothic UI, Meiryo UI, Microsoft YaHei UI, Segoe UI"); FontSize = 13; Background = Ui.Brush("#FAF9F6"); Foreground = Ui.Brush("#35372F");
            selected = CategoryThemes.Colors.Contains(color) ? color : CategoryThemes.Colors[0];
            var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };
            Content = panel;
            panel.Children.Add(new TextBlock { Text = Loc.T("Categories.Name"), Foreground = Ui.Brush("#5C5F56") });
            var box = new TextBox
            {
                Text = name, FontSize = 14, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Padding = new Thickness(0), MaxLength = 16, VerticalContentAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(new Border
            {
                Background = Brushes.White, BorderBrush = Ui.Brush("#E4E3DB"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 8, 0, 18), Child = box
            });
            panel.Children.Add(new TextBlock { Text = Loc.T("Categories.AccentColor"), Foreground = Ui.Brush("#5C5F56") });
            var swatches = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            foreach (var hex in CategoryThemes.Colors)
            {
                var swatch = new Button
                {
                    Width = 28, Height = 28, Margin = new Thickness(0, 0, 10, 0), Tag = hex, Cursor = Cursors.Hand,
                    FocusVisualStyle = null, Background = Ui.Brush(hex), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(2),
                    Template = SwatchTemplate, ToolTip = Loc.T("Colors.SelectTooltip")
                };
                swatch.Click += (_, _) => Select(swatches, (string)swatch.Tag);
                swatches.Children.Add(swatch);
            }
            panel.Children.Add(swatches);
            var error = new TextBlock { Foreground = Ui.Brush("#AA4545"), TextWrapping = TextWrapping.Wrap, MinHeight = 20, Margin = new Thickness(0, 12, 0, 0) };
            panel.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            var cancel = RoundButton(Loc.T("Common.Cancel"), "#F1F0EA");
            cancel.IsCancel = true; cancel.Margin = new Thickness(0, 0, 8, 0);
            var confirm = RoundButton(confirmText, "#DDE6CB");
            confirm.IsDefault = true;
            confirm.Click += (_, _) =>
            {
                var message = validate(box.Text);
                if (message != null) { error.Text = message; box.Focus(); return; }
                Result = new CategoryDraft(box.Text.Trim(), selected);
                DialogResult = true;
            };
            buttons.Children.Add(cancel); buttons.Children.Add(confirm); panel.Children.Add(buttons);
            Select(swatches, selected);
            Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        }

        private void Select(Panel swatches, string hex)
        {
            selected = hex;
            foreach (var child in swatches.Children)
            {
                if (child is not Button button) continue;
                button.BorderBrush = (string)button.Tag == hex ? Ui.Brush("#35372F") : Brushes.Transparent;
            }
        }

        private static Button RoundButton(string text, string fill) => new()
        {
            Content = text, MinWidth = 88, Padding = new Thickness(16, 8, 16, 8), Cursor = Cursors.Hand,
            Background = Ui.Brush(fill), Foreground = Ui.Brush("#35372F"), BorderThickness = new Thickness(0), FocusVisualStyle = null,
            Template = CreateButtonTemplate()
        };

        private static ControlTemplate CreateButtonTemplate()
        {
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.PaddingProperty, new Thickness(16, 8, 16, 8));
            border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            return new ControlTemplate(typeof(Button)) { VisualTree = border };
        }

        private static ControlTemplate CreateSwatchTemplate()
        {
            var ring = new FrameworkElementFactory(typeof(Border));
            ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            ring.SetValue(Border.PaddingProperty, new Thickness(2));
            ring.SetBinding(Border.BorderBrushProperty, new Binding(nameof(Control.BorderBrush)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            ring.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(Control.BorderThickness)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            var fill = new FrameworkElementFactory(typeof(Border));
            fill.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            fill.SetBinding(Border.BackgroundProperty, new Binding(nameof(Control.Background)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            ring.AppendChild(fill);
            return new ControlTemplate(typeof(Button)) { VisualTree = ring };
        }
    }
}
