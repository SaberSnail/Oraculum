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
			var paragraph = new Paragraph();
			paragraph.Inlines.Add(new Run(result.Message));
			AddParagraph(paragraph);
		}

		private void AddParagraph(Paragraph paragraph)
		{
			paragraph.Loaded += OnParagraphLoaded;
			static void OnParagraphLoaded(object sender, RoutedEventArgs args)
			{
				var p = (Paragraph) sender;
				p.Loaded -= OnParagraphLoaded;
				p.BringIntoView();
			}

			Document.Blocks.Add(paragraph);
		}
	}
}
