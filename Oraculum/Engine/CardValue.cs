using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public sealed class CardValue : RandomValueBase
{
	public CardValue(params int[] values)
		: this((IReadOnlyList<int>) values)
	{
	}

	public CardValue(IReadOnlyList<int> values)
		: base(values)
	{
		ShortText = values.Select(CardUtility.GetShortText).Join(" ");
		DisplayText = values.Select(CardUtility.GetDisplayText).Join(", ");
	}

	public override RandomValueKind Kind => RandomValueKind.Card;

	public override RandomValueBase? GetNextValue(IReadOnlyList<int> configurations) =>
		CardUtility.GetNextValue(this, configurations);

	public override string ShortText { get; }
	public override string DisplayText { get; }
}
