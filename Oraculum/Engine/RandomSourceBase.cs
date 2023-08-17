using System;
using System.Collections.Generic;
using Oraculum.Data;
using Oraculum.ViewModels;

namespace Oraculum.Engine;

public abstract class RandomSourceBase
{
	public static RandomSourceBase Create(RandomSourceData data)
	{
		return data.Kind switch
		{
			RandomSourceKind.Die => new DieSource(data.Dice[0]),
			RandomSourceKind.Card => new CardSource(false),
			_ => throw new NotImplementedException($"Unimplemented random source kind: {data.Kind}"),
		};
	}

	public abstract RandomSourceKind Kind { get; }
	public abstract string InputHintText { get; }
	public abstract RandomValueBase? TryConvertToValue(string input);
	public abstract IEnumerable<RandomValueBase> GetPossibleValues();
	
	public RandomValueBase GetRandomValue() => GetRandomValueCore();

	protected abstract RandomValueBase GetRandomValueCore();
}
