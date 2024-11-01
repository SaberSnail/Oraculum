using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public sealed class CardSequenceSource : RandomSourceBase
{
	public CardSequenceSource(IEnumerable<int> configurations)
		: base(configurations)
	{
		Configurations = configurations.Cast<CardSourceConfiguration>().AsReadOnlyList();
		InputHintText = Configurations
			.Select(CardUtility.GetHintTextForConfiguration)
			.Aggregate((joined, next) => string.Format(OurResources.CardSequenceInputHint, joined, next));
	}

	public new IReadOnlyList<CardSourceConfiguration> Configurations { get; }

	public override RandomSourceKind Kind => RandomSourceKind.CardSequence;

	public override string InputHintText { get; }

	public override RandomValueBase? TryConvertToValue(string input)
	{
		var tokens = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
		var values = new List<int>();

		if (Configurations.Count != tokens.Length)
			return null;

		for (int index = 0; index < tokens.Length; index++)
		{
			var (value, _) = CardUtility.TryParseSingleValue(tokens[index], Configurations[index]);
			if (value is null)
				return null;
			values.Add(value.Value);
		}

		return new CardValue(values);
	}

	public override RandomValueBase? ToValue(IReadOnlyList<int> values) =>
		new CardValue(values);

	public override IEnumerable<RandomValueBase> GetPossibleValues() =>
		CardUtility.GetAllValues(Configurations);

	public new CardValue GetRandomValue()
	{
		var values = Configurations
			.Select(CardUtility.GetSingleRandomValue)
			.ToList();
		return new CardValue(values);
	}

	protected override RandomValueBase GetRandomValueCore() => GetRandomValue();
}
