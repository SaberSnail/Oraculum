using System;
using System.Collections.Generic;
using GoldenAnvil.Utility;
using Oraculum.Data;

namespace Oraculum.Engine;

public abstract class RandomSourceBase
{
	public static RandomSourceBase Create(RandomPlan plan)
	{
		return plan.Kind switch
		{
			RandomSourceKind.DiceSum => new DiceSumSource(plan.Configurations),
			RandomSourceKind.DiceSequence => new DiceSequenceSource(plan.Configurations),
			RandomSourceKind.CardSequence => new CardSequenceSource(plan.Configurations),
			_ => throw new NotImplementedException($"Unimplemented random source kind: {plan.Kind}"),
		};
	}

	public IReadOnlyList<int> Configurations { get; init; }

	public abstract RandomSourceKind Kind { get; }
	public abstract string InputHintText { get; }
	public abstract RandomValueBase? TryConvertToValue(string input);
	public abstract RandomValueBase? ToValue(IReadOnlyList<int> value);
	public abstract IEnumerable<RandomValueBase> GetPossibleValues();
	
	public RandomValueBase GetRandomValue() => GetRandomValueCore();

	public IEnumerable<RandomValueBase> EnumerateValues(IReadOnlyList<int> startValues, IReadOnlyList<int> endValues)
	{
		var startValue = ToValue(startValues)!;
		var endValue = ToValue(endValues)!;
		return startValue.EnumerateTo(endValue, Configurations);
	}

	protected RandomSourceBase(IEnumerable<int> configurations)
	{
		Configurations = configurations.AsReadOnlyList();
	}

	protected abstract RandomValueBase GetRandomValueCore();
}
