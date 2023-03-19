using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GoldenAnvil.Utility;
using Oraculum.Data;

namespace Oraculum.Engine
{
  public sealed class RowManager
  {
    public RowManager(IReadOnlyList<RowData> rows)
    {
      m_outputLookup = rows
      .SelectMany(x => Enumerable.Range(x.Min, x.Max - x.Min + 1).Select(y => (Key: y, Value: x.Output)))
      .ToDictionary(x => (object) x.Key, x => x.Value)
      .AsReadOnly();
    }

    public string? GetOutput(object? key) => key is null ? null : m_outputLookup.GetValueOrDefault(key);

    readonly ReadOnlyDictionary<object, string> m_outputLookup;
  }

  public enum RandomSourceKind
  {
    Dice,
  }
  public abstract class RandomSourceBase
  {
    public abstract RandomSourceKind Kind { get; }

		public abstract (object Key, IReadOnlyList<object> Values) GenerateResult();
  }

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

    public IReadOnlyList<int> Dice { get; }

    public override (object Key, IReadOnlyList<object> Values) GenerateResult()
    {
      var values = Dice.Select(x => (object) Random.Shared.NextRoll(1, x)).AsReadOnlyList();
      var key = ValuesToKey(values);
      return (key, values);
    }
  }
}
