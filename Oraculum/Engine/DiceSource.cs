using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine
{
	public sealed class DiceSource : RandomSourceBase
  {
		public static object ValuesToKey(IReadOnlyList<object> values)
		{
			return values.Count switch
			{
				1 => values[0],
				2 => (values[0], values[1]),
				3 => (values[0], values[1], values[2]),
				4 => (values[0], values[1], values[2], values[3]),
				5 => (values[0], values[1], values[2], values[3], values[4]),
				6 => (values[0], values[1], values[2], values[3], values[4], values[5]),
				7 => (values[0], values[1], values[2], values[3], values[4], values[5], values[6]),
				8 => (values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]),
				9 => (values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8]),
				10 => (values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9]),
				_ => throw new ArgumentException($"Unsupported number of dice: {values.Count}", nameof(values)),
			};
		}

		public DiceSource(params int[] dice)
    {
      Dice = dice.AsReadOnlyList();
    }

    public override RandomSourceKind Kind => RandomSourceKind.Dice;

    public IReadOnlyList<int> Dice { get; init; }

    public override (object Key, IReadOnlyList<object> Values) GenerateResult()
    {
      var values = Dice.Select(x => (object) Random.Shared.NextRoll(1, x)).AsReadOnlyList();
      var key = ValuesToKey(values);
      return (key, values);
    }
  }
}
