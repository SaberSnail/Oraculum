using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public sealed class FixedValue : RandomValueBase
{
	public FixedValue(int count)
		: base(Enumerable.Repeat(1, count).AsReadOnlyList())
	{
	}

	public override RandomValueKind Kind => RandomValueKind.Fixed;

	public override string ShortText => "";

	public override string DisplayText => "";

	public override RandomValueBase? GetNextValue(IReadOnlyList<int> configurations) => null;
}
