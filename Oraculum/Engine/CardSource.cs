using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public sealed class CardSource : RandomSourceBase
{
	public CardSource(bool includeJokers)
	{
		IncludeJokers = includeJokers;
	}

	public bool IncludeJokers { get; }

	public override RandomSourceKind Kind => RandomSourceKind.Card;

	public override string InputHintText => OurResources.CardInputHint;

	public override RandomValueBase? TryConvertToValue(string input)
	{
		var value = CardUtility.TryParse(input);
		if (value is null)
			return null;
		if (IncludeJokers && CardUtility.IsJoker(value.Value))
			return null;
		return new CardValue(value.Value);
	}

	public override IEnumerable<RandomValueBase> GetPossibleValues() =>
		Enumerable.Range(1, IncludeJokers ? 54 : 52).Select(x => new CardValue(x));

	public new CardValue GetRandomValue() =>
		new CardValue(Random.Shared.NextRoll(1, IncludeJokers ? 54 : 52));

	protected override RandomValueBase GetRandomValueCore() => GetRandomValue();
}
