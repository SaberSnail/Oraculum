using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public sealed class DiceSumSource : RandomSourceBase
{
	public static int? MergeConfigurations(int config1, int config2) => Math.Max(config1, config2);

	public DiceSumSource(IEnumerable<int> configurations)
		: base(configurations)
	{
		Sides = configurations.AsReadOnlyList();
		InputHintText = Sides
			.GroupBy(x => x)
			.OrderByDescending(x => x.Key)
			.Select(x => string.Format(OurResources.SingleDieInputHint, x))
			.Aggregate((joined, next) => string.Format(OurResources.DiceSumInputHint, joined, next));
	}

	public IReadOnlyList<int> Sides { get; }

	public override RandomSourceKind Kind => RandomSourceKind.DiceSum;

	public override string InputHintText { get; }

	public override RandomValueBase? TryConvertToValue(string input)
	{
		var tokens = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
		var values = new List<int>();
		foreach (var token in tokens)
		{
			if (!int.TryParse(token, CultureInfo.CurrentCulture, out var value))
				return null;
			values.Add(value);
		}

		if (values.Count == 0)
		{
			return null;
		}
		else
		{
			if (values.Count != Sides.Count || values.Sum() > Sides.Sum())
				return null;
		}

		return new DieValue(values.Sum());
	}

	public override RandomValueBase? ToValue(IReadOnlyList<int> values) =>
		new DieValue(values.Sum());

	public override IEnumerable<RandomValueBase> GetPossibleValues() =>
		Enumerable.Range(Sides.Count, Sides.Sum()).Select(x => new DieValue(x));

	public new DieValue GetRandomValue()
	{
		var total = Sides.Sum(DieUtility.GetSingleRandomValue);
		return new DieValue(total);
	}

	protected override RandomValueBase GetRandomValueCore() => GetRandomValue();
}
