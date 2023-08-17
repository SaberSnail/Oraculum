using System;

namespace Oraculum.Engine;

public sealed class CardValue : RandomValueBase
{
	public CardValue(int value)
		: base(value)
	{
		if (value < 1 || value > 54)
			throw new ArgumentOutOfRangeException(nameof(value), value, null);

		DisplayText = CardUtility.GetDisplayText(value);
	}

	public override string DisplayText { get; }
}
