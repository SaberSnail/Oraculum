using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Oraculum.Engine;

[DebuggerDisplay("{ToInt()}")]
public sealed class DigitCollection : IEnumerable<int>, IEquatable<DigitCollection>, IComparable<DigitCollection>
{
	public static DigitCollection? TryCreate(string input)
	{
		try
		{
			var digits = input
				.Select(digit => int.Parse(digit.ToString(CultureInfo.InvariantCulture)))
				.ToList();
			return new DigitCollection(digits);
		}
		catch (FormatException)
		{
			return null;
		}
	}

	public static DigitCollection Create(int input)
	{
		var digits = input.ToString(CultureInfo.InvariantCulture)
			.Select(digit => int.Parse(digit.ToString(CultureInfo.InvariantCulture)))
			.ToList();
		return new DigitCollection(digits);
	}

	public int Count => m_digits.Count;

	public int ToInt()
	{
		var value = 0;
		var multiple = 1;
		foreach (var digit in m_digits)
		{
			value += digit * multiple;
			multiple *= 10;
		}
		return value;
	}

	public override bool Equals(object? that) => Equals(that as DigitCollection);

	public bool Equals(DigitCollection? that)
	{
		if (that is null)
			return false;

		return m_digits.SequenceEqual(that.m_digits);
	}

	public int CompareTo(DigitCollection? that)
	{
		if (that is null)
			return 1;
		if (Count > that.Count)
			return 1;
		if (Count < that.Count)
			return -1;
		for (var index = Count - 1; index >= 0; index--)
		{
			if (this[index] > that[index])
				return 1;
			if (this[index] < that[index])
				return -1;
		}
		return 0;
	}

	public int this[int index]
	{
		get => m_digits[index];
		set => m_digits[index] = value;
	}

	public override int GetHashCode() => ToInt().GetHashCode();

	public IEnumerator<int> GetEnumerator() => m_digits.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) m_digits).GetEnumerator();

	public static implicit operator int(DigitCollection digits) => digits.ToInt();
	public static implicit operator DigitCollection(int number) => Create(number);

	private DigitCollection(List<int> digits)
	{
		m_digits = digits;
	}

	private List<int> m_digits;
}
