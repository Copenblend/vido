using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Vido.Views.Services;

/// <summary>
/// Converts Markdown text to WPF UI elements using Markdig for parsing.
/// Renders headings, paragraphs, lists, code blocks, bold, italic, inline code, and links.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// Renders Markdown text into a StackPanel of WPF elements.
    /// </summary>
    /// <param name="markdown">The raw Markdown text to parse and render.</param>
    public static UIElement Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return CreateTextBlock("No content available.", isMuted: true);

        var document = Markdown.Parse(markdown, Pipeline);
        var panel = new StackPanel { Margin = new Thickness(0) };

        foreach (var block in document)
        {
            var element = RenderBlock(block);
            if (element is not null)
                panel.Children.Add(element);
        }

        return panel;
    }

    private static UIElement? RenderBlock(Markdig.Syntax.Block block)
    {
        return block switch
        {
            HeadingBlock heading => RenderHeading(heading),
            ParagraphBlock paragraph => RenderParagraph(paragraph),
            ListBlock list => RenderList(list),
            FencedCodeBlock codeBlock => RenderCodeBlock(codeBlock),
            CodeBlock codeBlock => RenderCodeBlock(codeBlock),
            ThematicBreakBlock => RenderHorizontalRule(),
            QuoteBlock quote => RenderQuote(quote),
            _ => null
        };
    }

    private static UIElement RenderHeading(HeadingBlock heading)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 4),
            FontFamily = new FontFamily("Segoe UI"),
            FontWeight = FontWeights.Bold,
        };

        tb.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryForegroundBrush");

        tb.FontSize = heading.Level switch
        {
            1 => 20,
            2 => 17,
            3 => 15,
            _ => 14
        };

        if (heading.Inline is not null)
            RenderInlines(tb.Inlines, heading.Inline);

        return tb;
    }

    private static UIElement RenderParagraph(ParagraphBlock paragraph)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryForegroundBrush");

        if (paragraph.Inline is not null)
            RenderInlines(tb.Inlines, paragraph.Inline);

        return tb;
    }

    private static UIElement RenderList(ListBlock list)
    {
        var panel = new StackPanel { Margin = new Thickness(16, 0, 0, 8) };
        int index = 1;

        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem) continue;

            var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            var bullet = new TextBlock
            {
                Text = list.IsOrdered ? $"{index++}. " : "• ",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = 16,
            };
            bullet.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryForegroundBrush");
            itemPanel.Children.Add(bullet);

            var contentPanel = new StackPanel();
            foreach (var subBlock in listItem)
            {
                var rendered = RenderBlock(subBlock);
                if (rendered is not null)
                    contentPanel.Children.Add(rendered);
            }
            itemPanel.Children.Add(contentPanel);
            panel.Children.Add(itemPanel);
        }

        return panel;
    }

    private static UIElement RenderCodeBlock(Markdig.Syntax.Block codeBlock)
    {
        string code;
        if (codeBlock is FencedCodeBlock fenced)
        {
            code = string.Join('\n', fenced.Lines);
        }
        else if (codeBlock is CodeBlock indented)
        {
            code = string.Join('\n', indented.Lines);
        }
        else
        {
            code = string.Empty;
        }

        var tb = new TextBlock
        {
            Text = code.TrimEnd(),
            FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 4, 0, 8),
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty, "PrimaryForegroundBrush");

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a)),
            CornerRadius = new CornerRadius(4),
            Child = tb,
        };

        return border;
    }

    private static UIElement RenderHorizontalRule()
    {
        var separator = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 8, 0, 8),
        };
        separator.SetResourceReference(Border.BackgroundProperty, "PrimaryBorderBrush");
        return separator;
    }

    private static UIElement RenderQuote(QuoteBlock quote)
    {
        var contentPanel = new StackPanel();
        foreach (var block in quote)
        {
            var rendered = RenderBlock(block);
            if (rendered is not null)
                contentPanel.Children.Add(rendered);
        }

        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 4, 0, 4),
            Margin = new Thickness(0, 4, 0, 8),
            Child = contentPanel,
        };
        border.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");

        return border;
    }

    private static void RenderInlines(InlineCollection inlines, ContainerInline container)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    inlines.Add(new Run(literal.Content.ToString()));
                    break;

                case EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterCount == 2 || emphasis.DelimiterChar == '*' && emphasis.DelimiterCount >= 2)
                        span.FontWeight = FontWeights.Bold;
                    else
                        span.FontStyle = FontStyles.Italic;
                    RenderInlines(span.Inlines, emphasis);
                    inlines.Add(span);
                    break;

                case CodeInline code:
                    var codeRun = new Run(code.Content)
                    {
                        FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                        FontSize = 12,
                        Background = new SolidColorBrush(Color.FromRgb(0x2a, 0x2a, 0x2a)),
                    };
                    inlines.Add(codeRun);
                    break;

                case LinkInline link:
                    var hyperlink = new Span();
                    hyperlink.SetResourceReference(TextElement.ForegroundProperty, "LinkForegroundBrush");
                    RenderInlines(hyperlink.Inlines, link);
                    inlines.Add(hyperlink);
                    break;

                case LineBreakInline:
                    inlines.Add(new LineBreak());
                    break;

                case HtmlInline:
                    // Skip HTML inlines
                    break;

                case ContainerInline nestedContainer:
                    RenderInlines(inlines, nestedContainer);
                    break;
            }
        }
    }

    private static TextBlock CreateTextBlock(string text, bool isMuted = false)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
        };
        tb.SetResourceReference(TextBlock.ForegroundProperty,
            isMuted ? "SecondaryForegroundBrush" : "PrimaryForegroundBrush");
        return tb;
    }
}
