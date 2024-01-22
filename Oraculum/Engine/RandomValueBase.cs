using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public abstract class RandomValueBase : IEquatable<RandomValueBase>
{
	public static (RandomValueBase? Value, int? Config) TryParseSingleValue(string input)
	{
		int? value;
		int? config;

		(value, config) = DieUtility.TryParseSingleValue(input, null);
		if (value is not null)
			return (new DieValue(value.Value), config);

		(value, config) = CardUtility.TryParseSingleValue(input, null);
		if (value is not null)
			return (new CardValue(value.Value), config);

		return (null, null);
	}

	protected RandomValueBase(IReadOnlyList<int> values)
	{
		if (values is null)
			throw new ArgumentNullException(nameof(values));
		if (values.Count == 0)
			throw new ArgumentException("Must have at least one value.", nameof(values));

		Values = values;
	}

	public abstract RandomValueKind Kind { get; }

	public IReadOnlyList<int> Values { get; }

	public abstract string ShortText { get; }

	public abstract string DisplayText { get; }

	public abstract RandomValueBase? GetNextValue(IReadOnlyList<int> configurations);

	public bool IsImmediatelyAfter(RandomValueBase that, IReadOnlyList<int> configurations) =>
		Equals(that?.GetNextValue(configurations));

	public IEnumerable<RandomValueBase> EnumerateTo(RandomValueBase lastValue, IReadOnlyList<int> configurations)
	{
		yield return this;
		RandomValueBase? currentValue = this;
		while (currentValue! != lastValue)
		{
			currentValue = currentValue.GetNextValue(configurations);
			if (currentValue is null)
				yield break;
			yield return currentValue;
		}
	}

	public override bool Equals(object? that) => Equals(that as RandomValueBase);

	public bool Equals(RandomValueBase? that) =>
		that is { } && Kind == that.Kind && Values.SequenceEqual(that.Values);

	public override int GetHashCode() => HashCodeUtility.CombineHashCodes(Kind.GetHashCode(), HashCodeUtility.CombineHashCodes(Values.ToArray()));

	public static bool operator ==(RandomValueBase left, RandomValueBase right) =>
		left.Equals(right);

	public static bool operator !=(RandomValueBase left, RandomValueBase right) =>
		!left.Equals(right);

	public override string ToString() => ShortText;
}
