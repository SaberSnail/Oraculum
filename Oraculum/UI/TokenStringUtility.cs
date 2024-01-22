using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Documents;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Windows;
using Oraculum.Data;

namespace Oraculum.UI;

public static class TokenStringUtility
{
	public static IEnumerable<Inline> TokenStringToInlines(string text, string textStyle)
	{
		var currentIndex = 0;
		foreach (var match in s_tokenRegex.Matches(text).Cast<Match>())
		{
			if (match.Index > currentIndex)
				yield return new Run(text.Substring(currentIndex, match.Index - currentIndex)).WithStyle(textStyle);

			var token = match.Groups[1].Captures[0].ToString();
			TableReference? tableReference = null;
			if (Guid.TryParse(token, out var tableId))
				tableReference = AppModel.Instance.Data.GetTableReference(tableId);
			if (tableReference is not null)
			{
				var hyperlinkRun = new Run(tableReference.Title);
				var hyperlink = new Hyperlink(hyperlinkRun)
				{
					Command = new DelegateCommand(() => AppModel.Instance.OpenTable(tableReference))
				};
				yield return hyperlink;
			}
			else
			{
				yield return new Run(OurResources.UnknownTableName).WithStyle("UnknownTableRunStyle");
			}

			currentIndex = match.Index + match.Length;
		}

		if (currentIndex < text.Length)
			yield return new Run(text.Substring(currentIndex)).WithStyle(textStyle);
	}

	public static IEnumerable<TableReference> GetTableReferences(string text)
	{
		return s_tokenRegex.Matches(text)
			.Cast<Match>()
			.Select(match => match.Groups[1].Captures[0].ToString())
			.Select(token =>
			{
				if (Guid.TryParse(token, out var tableId))
					return AppModel.Instance.Data.GetTableReference(tableId);
				return null;
			})
			.WhereNotNull()
			.Cast<TableReference>();
	}

	private static readonly Regex s_tokenRegex = new Regex(@"\{([0-9A-Fa-f]{8}[-]?(?:[0-9A-Fa-f]{4}[-]?){3}[0-9A-Fa-f]{12})\}");
}
