using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public sealed class DieSource : RandomSourceBase
{
	public DieSource(int sides)
	{
		Sides = sides;
	}

	public int Sides { get; }

	public override RandomSourceKind Kind => RandomSourceKind.Die;

	public override string InputHintText => string.Format(OurResources.SingleDieInputHint, Sides);

	public override RandomValueBase? TryConvertToValue(string input)
	{
		if (!int.TryParse(input, CultureInfo.CurrentCulture, out var value))
			return null;
		return new DieValue(value);
	}

	public override IEnumerable<RandomValueBase> GetPossibleValues() =>
		Enumerable.Range(1, Sides).Select(x => new DieValue(x));

	public new DieValue GetRandomValue() =>
		new DieValue(Random.Shared.NextRoll(1, Sides));

	protected override RandomValueBase GetRandomValueCore() => GetRandomValue();
}
