using System;
using System.Windows;
using System.Windows.Documents;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Windows;
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
			if (result.TableId != m_lastRollResultTableId)
			{
				var tableParagraph = new Paragraph().WithStyle("RollResultTableParagraphStyle");
				tableParagraph.Inlines.AddRange(TextElementUtility.FormatInlineString(
					OurResources.RollResultTable,
					null,
					new Hyperlink(new Run(result.TableTitle).WithStyle("RollResultTableTitleRunStyle"))
					{
						Command = new DelegateCommand(() => AppModel.Instance.OpenTable(result.TableId))
					},
					new Run(result.TableTitle).WithStyle("RollResultTableTitleRunStyle")
					));
				AddParagraph(tableParagraph, false);
			}

			var paragraph = new Paragraph().WithStyle("RollResultParagraphStyle");
			paragraph.Inlines.AddRange(TextElementUtility.FormatInlineString(
				OurResources.RollResultKey,
				null,
				new Run(result.Key).WithStyle("RollResultKeyRunStyle")
				));
			paragraph.Inlines.Add(new LineBreak());
			paragraph.Inlines.Add(new Run(result.Output).WithStyle("RollResultOutputRunStyle"));
			AddParagraph(paragraph, true);

			m_lastRollResultTableId = result.TableId;
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

		private Guid? m_lastRollResultTableId;
	}
}
