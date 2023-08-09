using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Windows;
using GoldenAnvil.Utility.Windows.Async;
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

		public void RollStarted(Guid tableId, string tableTitle)
		{
			if (tableId != m_lastRollResultTableId)
			{
				var tableParagraph = new Paragraph().WithStyle("RollResultTableParagraphStyle");
				tableParagraph.Inlines.AddRange(TextElementUtility.FormatInlineString(
					OurResources.RollResultTable,
					null,
					new Hyperlink(new Run(tableTitle).WithStyle("RollResultTableTitleRunStyle"))
					{
						Command = new DelegateCommand(() => AppModel.Instance.OpenTable(tableId))
					}
					));
				AddParagraph(tableParagraph, true);
			}
		}

		public async Task AddAsync(TaskStateController state, RollResult result)
		{
			var paragraph = new Paragraph().WithStyle("RollResultParagraphStyle");
			paragraph.Inlines.AddRange(TextElementUtility.FormatInlineString(
				OurResources.RollResultKey,
				null,
				new Run(result.Key).WithStyle("RollResultKeyRunStyle")
				));
			paragraph.Inlines.Add(new LineBreak());
			paragraph.Inlines.Add(new Run(result.Output).WithStyle("RollResultOutputRunStyle"));

			if (result.Next is not null)
			{
				var nextTableMetadata = await AppModel.Instance.Data.GetTableMetadataAsync(result.Next.Value, state.CancellationToken).ConfigureAwait(false);
				paragraph.Inlines.Add(new LineBreak());
				paragraph.Inlines.AddRange(TextElementUtility.FormatInlineString(
					"also roll on {0}",
					null,
					new Hyperlink(new Run(nextTableMetadata?.Title ?? "<unknown>").WithStyle("RollResultOutputRunStyle"))
					{
						Command = new DelegateCommand(() => AppModel.Instance.OpenTable(result.Next.Value))
					}
					));
			}

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
