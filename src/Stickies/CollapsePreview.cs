using System.Collections.Generic;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PaperNotes;

/// <summary>First-line rich preview for the collapsed strip. Clones Inlines; never reparents live nodes.</summary>
public static class CollapsePreview
{
    public static void Fill(System.Windows.Controls.TextBlock target, FlowDocument? document)
    {
        target.Inlines.Clear();
        if (document?.Blocks.FirstBlock is not Paragraph paragraph) return;

        bool stop = false;
        foreach (var inline in paragraph.Inlines)
        {
            AppendClones(target.Inlines, inline, ref stop);
            if (stop) break;
        }
    }

    static void AppendClones(InlineCollection dest, Inline inline, ref bool stop)
    {
        if (stop) return;
        if (inline is LineBreak)
        {
            stop = true;
            return;
        }
        // Skip interactive task chrome; keep only body text/formatting.
        if (inline is InlineUIContainer) return;

        if (inline is Run run)
        {
            var clone = new Run(run.Text);
            CopyTextElement(run, clone);
            dest.Add(clone);
            return;
        }

        if (inline is Span span)
        {
            var clone = CreateSpanShell(span);
            CopyTextElement(span, clone);
            bool childStop = false;
            foreach (var child in span.Inlines)
            {
                AppendClones(clone.Inlines, child, ref childStop);
                if (childStop)
                {
                    stop = true;
                    break;
                }
            }
            dest.Add(clone);
        }
    }

    static Span CreateSpanShell(Span span) => span switch
    {
        Bold => new Bold(),
        Italic => new Italic(),
        Underline => new Underline(),
        _ => new Span()
    };

    static void CopyTextElement(TextElement source, TextElement target)
    {
        if (source.ReadLocalValue(TextElement.FontFamilyProperty) != DependencyProperty.UnsetValue)
            target.FontFamily = source.FontFamily;
        if (source.ReadLocalValue(TextElement.FontSizeProperty) != DependencyProperty.UnsetValue)
            target.FontSize = source.FontSize;
        if (source.ReadLocalValue(TextElement.FontWeightProperty) != DependencyProperty.UnsetValue)
            target.FontWeight = source.FontWeight;
        if (source.ReadLocalValue(TextElement.FontStyleProperty) != DependencyProperty.UnsetValue)
            target.FontStyle = source.FontStyle;
        if (source.ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue)
            target.Foreground = CloneBrush(source.Foreground);
        if (source.ReadLocalValue(TextElement.BackgroundProperty) != DependencyProperty.UnsetValue)
            target.Background = CloneBrush(source.Background);
        if (source is Inline srcInline && target is Inline dstInline
            && srcInline.ReadLocalValue(Inline.TextDecorationsProperty) != DependencyProperty.UnsetValue
            && srcInline.TextDecorations != null)
            dstInline.TextDecorations = srcInline.TextDecorations.CloneCurrentValue();
    }

    static Brush? CloneBrush(Brush? brush)
    {
        if (brush == null) return null;
        if (brush.IsFrozen) return brush;
        var copy = brush.CloneCurrentValue();
        if (copy.CanFreeze) copy.Freeze();
        return copy;
    }
}
