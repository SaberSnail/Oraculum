using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using Oraculum.Engine;

namespace Oraculum.ViewModels
{
	public class RollLogViewModel : ViewModelBase
	{
		public RollLogViewModel()
		{
			Document = new FlowDocument();
		}

		public FlowDocument Document { get; }

		public void Add(RollResult result)
		{
			if (result.TableTitle != m_lastRollResultTableTitle)
			{
				var tableParagraph = new Paragraph();
				tableParagraph.Style = (Style) Application.Current.FindResource("RollResultTableParagraphStyle");
				tableParagraph.Inlines.Add(new Run("Rolling on "));
				tableParagraph.Inlines.Add(new Run
				{
					Text = result.TableTitle,
					Style = (Style) Application.Current.FindResource("RollResultTableTitleRunStyle"),
				});
				tableParagraph.Inlines.Add(new Run("…"));
				AddParagraph(tableParagraph, false);
			}

			var paragraph = new Paragraph();
			paragraph.Style = (Style) Application.Current.FindResource("RollResultParagraphStyle");
			paragraph.Inlines.Add(new Run("Rolled "));
			paragraph.Inlines.Add(new Run
			{
				Text = "\n\t" + result.Message,
				Style = (Style) Application.Current.FindResource("RollResultOutputRunStyle"),
			});
			AddParagraph(paragraph, true);

			m_lastRollResultTableTitle = result.TableTitle;
		}

		private void AddParagraph(Paragraph paragraph, bool shouldBringIntoView)
		{
			if (shouldBringIntoView)
			{
				paragraph.Loaded += OnParagraphLoaded;
				static void OnParagraphLoaded(object sender, RoutedEventArgs args)
				{
					var p = (Paragraph) sender;
					p.Loaded -= OnParagraphLoaded;
					p.BringIntoView();
				}
			}

			Document.Blocks.Add(paragraph);
		}

		private string? m_lastRollResultTableTitle;
	}
}
