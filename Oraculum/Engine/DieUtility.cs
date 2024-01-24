using System.Globalization;
using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public static class DieUtility
{
	public static int GetSingleRandomValue(int config) => Random.Shared.NextRoll(1, config);

	public static IEnumerable<DieValue> GetAllValues(IEnumerable<int> configurations)
	{
		var configs = configurations.AsReadOnlyList();
		var currentValue = GetFirstValue(configs);
		var lastValue = GetLastValue(configs);
		while (true)
		{
			yield return currentValue;
			if (currentValue == lastValue)
				break;
			currentValue = GetNextValue(currentValue, configs)!;
		}
	}

	public static IEnumerable<int> GetAllValues(int config) => Enumerable.Range(1, config);

	public static DieValue? GetNextValue(DieValue value, IEnumerable<int> configurations)
	{
		var configs = configurations.AsReadOnlyList();
		var values = new List<int>(value.Values);

		if (values.Count == 1 && configs.Count > 1)
		{
			var nextValue = values[0] + 1;
			if (nextValue > configs.Sum(x => x))
				return null;
			return new DieValue(nextValue);
		}

		if (values.Count != configs.Count)
			throw new InvalidOperationException("Number of values must match number of configurations.");

		for (var index = values.Count - 1; index >= 0; index--)
		{
			var (nextValue, isOverflow) = GetNextValue(values[index], configs[index]);
			values[index] = nextValue;
			if (!isOverflow)
				return new DieValue(values);
		}

		return null;
	}

	public static DieValue GetFirstValue(IEnumerable<int> configurations) =>
		new DieValue(Enumerable.Repeat(1, configurations.Count()).AsReadOnlyList());

	public static DieValue GetLastValue(IEnumerable<int> configurations) =>
		new DieValue(configurations.AsReadOnlyList());

	public static (int Value, bool Overflow) GetNextValue(int value, int config) =>
		value >= config ? (1, true) : (value + 1, false);

	public static IReadOnlyList<int>? MergeConfigurations(IEnumerable<IReadOnlyList<int>> configs) =>
		RandomPlanUtility.MergeConfigurations(configs, MergeConfigurations);

	public static int? MergeConfigurations(int config1, int config2) => Math.Max(config1, config2);

	public static (int? Value, int? Config) TryParseSingleValue(string input, int? config)
	{
		if (!int.TryParse(input, CultureInfo.InvariantCulture, out var value))
			return (null, null);
		if (value < 0 || (config is not null && value > config.Value))
			return (null, null);

		if (value == 0)
		{
			if (input.All(c => c == '0'))
				value = (int) Math.Pow(10, input.Length);
		}

		var guessedConfig = config ?? GetNearestDieSides(value);
		if (guessedConfig is null)
			return (null, null);

		return (value, guessedConfig);

		static int? GetNearestDieSides(int value)
		{
			return value <= 4 ? 4 :
				value <= 6 ? 6 :
				value <= 8 ? 8 :
				value <= 10 ? 10 :
				value <= 12 ? 12 :
				value <= 20 ? 20 :
				value <= 100 ? 100 :
				value <= 1000 ? 1000 :
				null;
		}
	}
}
