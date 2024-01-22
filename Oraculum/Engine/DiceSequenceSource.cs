using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public sealed class DiceSequenceSource : RandomSourceBase
{
	public DiceSequenceSource(IEnumerable<int> configurations)
		: base(configurations)
	{
		Sides = configurations.AsReadOnlyList();
		InputHintText = Sides
			.Select(x => string.Format(OurResources.SingleDieInputHint, x))
			.Aggregate((joined, next) => string.Format(OurResources.DiceSequenceInputHint, joined, next));
	}

	public IReadOnlyList<int> Sides { get; }

	public override RandomSourceKind Kind => RandomSourceKind.DiceSequence;

	public override string InputHintText { get; }

	public override RandomValueBase? TryConvertToValue(string input)
	{
		var tokens = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
		var values = new List<int>();
		if (tokens.Length != Sides.Count)
			return null;

		for (var index = 0; index < tokens.Length; index++)
		{
			var (value, _) = DieUtility.TryParseSingleValue(tokens[index], Sides[index]);
			if (value is null)
				return null;
			values.Add(value.Value);
		}

		return new DieValue(values);
	}

	public override RandomValueBase? ToValue(IReadOnlyList<int> values) =>
		new DieValue(values);

	public override IEnumerable<RandomValueBase> GetPossibleValues() =>
		DieUtility.GetAllValues(Sides);

	public new DieValue GetRandomValue()
	{
		var values = Sides
			.Select(DieUtility.GetSingleRandomValue)
			.ToList();
		return new DieValue(values);
	}

	protected override RandomValueBase GetRandomValueCore() => GetRandomValue();
}
