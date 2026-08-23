using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using TextReaders.ViewModels;
using MdBlock = Markdig.Syntax.Block;
using WpfList = System.Windows.Documents.List;

namespace TextReaders.Services;

// Markdig의 파싱 결과(AST)를 FlowDocument로 변환한다. 헤딩/문단/굵게·기울임/목록/코드만 지원 - 표·이미지는 범위 밖.
// 기존 ReaderViewModel.BuildFlowDocument(평문 텍스트용)와 반환 모양을 맞춰서, LoadBook에서 확장자로만 분기하면 되게 한다.
public static class MarkdownFlowDocumentBuilder
{
    public static (FlowDocument Document, List<TocEntry> TocEntries) Build(string markdown)
    {
        var document = new FlowDocument();
        var tocEntries = new List<TocEntry>();

        var markdownDocument = Markdig.Markdown.Parse(markdown);
        foreach (var block in markdownDocument)
        {
            AppendBlock(document, block, tocEntries);
        }

        return (document, tocEntries);
    }

    private static void AppendBlock(FlowDocument document, MdBlock block, List<TocEntry> tocEntries)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var headingParagraph = new Paragraph
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = Math.Max(14, 24 - (heading.Level - 1) * 3),
                    Margin = new Thickness(0, 12, 0, 6),
                };
                AppendInlines(headingParagraph.Inlines, heading.Inline);
                document.Blocks.Add(headingParagraph);

                var title = new TextRange(headingParagraph.ContentStart, headingParagraph.ContentEnd).Text;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    tocEntries.Add(new TocEntry(title.Length > 40 ? title[..40] + "…" : title, headingParagraph));
                }

                break;

            case ParagraphBlock paragraphBlock:
                var paragraph = new Paragraph();
                AppendInlines(paragraph.Inlines, paragraphBlock.Inline);
                document.Blocks.Add(paragraph);
                break;

            case ListBlock listBlock:
                var list = new WpfList { MarkerStyle = listBlock.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc };
                foreach (var item in listBlock)
                {
                    if (item is not ListItemBlock listItemBlock)
                    {
                        continue;
                    }

                    var listItem = new ListItem();
                    foreach (var childBlock in listItemBlock)
                    {
                        if (childBlock is ParagraphBlock itemParagraphBlock)
                        {
                            var itemParagraph = new Paragraph();
                            AppendInlines(itemParagraph.Inlines, itemParagraphBlock.Inline);
                            listItem.Blocks.Add(itemParagraph);
                        }
                    }

                    list.ListItems.Add(listItem);
                }

                document.Blocks.Add(list);
                break;

            case FencedCodeBlock or CodeBlock:
                var codeText = ((LeafBlock)block).Lines.ToString();
                document.Blocks.Add(new Paragraph(new Run(codeText) { FontFamily = new FontFamily("Consolas") })
                {
                    Background = Brushes.WhiteSmoke,
                    Padding = new Thickness(8),
                });
                break;

            case QuoteBlock quoteBlock:
                foreach (var childBlock in quoteBlock)
                {
                    AppendBlock(document, childBlock, tocEntries);
                }

                break;

            case ThematicBreakBlock:
                document.Blocks.Add(new Paragraph(new Run("―――――――――――")) { Foreground = Brushes.Gray });
                break;

            // 표/이미지/HTML 블록 등은 범위 밖이라 조용히 건너뛴다.
        }
    }

    private static void AppendInlines(InlineCollection inlines, ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    inlines.Add(new Run(literal.Content.ToString()));
                    break;

                case EmphasisInline emphasis:
                    var span = new Span();
                    if (emphasis.DelimiterCount >= 2)
                    {
                        span.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        span.FontStyle = FontStyles.Italic;
                    }

                    AppendInlines(span.Inlines, emphasis);
                    inlines.Add(span);
                    break;

                case CodeInline code:
                    inlines.Add(new Run(code.Content) { FontFamily = new FontFamily("Consolas"), Background = Brushes.WhiteSmoke });
                    break;

                case LineBreakInline:
                    inlines.Add(new LineBreak());
                    break;

                case ContainerInline containerInline:
                    AppendInlines(inlines, containerInline);
                    break;
            }
        }
    }
}
