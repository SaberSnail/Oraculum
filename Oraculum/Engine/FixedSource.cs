using System;
using System.Collections.Generic;
using System.Linq;

namespace Oraculum.Engine;

public sealed class FixedSource : RandomSourceBase
{
	public FixedSource(int valueCount)
		: base(Enumerable.Repeat(1, valueCount))
	{
		m_fixedValue = new FixedValue(valueCount);
	}

	public override RandomSourceKind Kind => RandomSourceKind.Fixed;

	public override string InputHintText => "";

	public override RandomValueBase? TryConvertToValue(string input)
	{
		var tokens = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
		var values = new List<int>();
		if (tokens.Length != Configurations.Count)
			return null;
		return m_fixedValue;
	}

	public override RandomValueBase? ToValue(IReadOnlyList<int> value) => m_fixedValue;

	public override IEnumerable<RandomValueBase> GetPossibleValues() =>
		[m_fixedValue];

	protected override RandomValueBase GetRandomValueCore() => m_fixedValue;

	private FixedValue m_fixedValue;
}
