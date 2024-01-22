using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public sealed class DieValue : RandomValueBase
{
	public DieValue(params int[] values)
		: this((IReadOnlyList<int>) values)
	{
	}

	public DieValue(IReadOnlyList<int> values)
		: base(values)
	{
		ShortText = values.Select(x => x.ToString()).Join(",");
		DisplayText = values.Select(x => x.ToString()).Join(", ");
	}

	public override RandomValueKind Kind => RandomValueKind.Die;

	public override RandomValueBase? GetNextValue(IReadOnlyList<int> configurations) =>
		DieUtility.GetNextValue(this, configurations);

	public override string ShortText { get; }
	public override string DisplayText { get; }
}
