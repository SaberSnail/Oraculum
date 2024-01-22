using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;

namespace Oraculum.Engine;

public static class CardUtility
{
	public const int BlackJokerValue = 53;
	public const int RedJokerValue = 54;

	public const int BlackValue = 55;
	public const int RedValue = 56;

	public const int ClubValue = 57;
	public const int DiamondValue = 58;
	public const int HeartValue = 59;
	public const int SpadeValue = 60;

	public const int FirstRankValue = 61;
	public const int LastRankValue = 73;

	public static bool IsJoker(int value) =>
		value == BlackJokerValue || value == RedJokerValue;

	public static bool IsColor(int value) =>
		value == BlackValue || value == RedValue;

	public static bool IsSuit(int value) =>
		value == ClubValue || value == DiamondValue || value == HeartValue || value == SpadeValue;

	public static bool IsRank(int value) =>
		value >= FirstRankValue && value <= LastRankValue;

	public static int GetSingleRandomValue(int config) =>
		GetSingleRandomValue((CardSourceConfiguration) config);

	public static int GetSingleRandomValue(CardSourceConfiguration config)
	{
		return config switch
		{
			CardSourceConfiguration.FullDeck => Random.Shared.NextRoll(1, 52),
			CardSourceConfiguration.FullDeckWithJokers => Random.Shared.NextRoll(1, 54),
			CardSourceConfiguration.Suits => GetRandomSuitValue(),
			CardSourceConfiguration.Ranks => Random.Shared.NextRoll(1, 13) + FirstRankValue - 1,
			_ => throw new NotImplementedException($"Unimplemented card source configuration: {config}"),
		};

		static int GetRandomSuitValue()
		{
			return Random.Shared.NextRoll(1, 4) switch
			{
				1 => ClubValue,
				2 => DiamondValue,
				3 => HeartValue,
				4 => SpadeValue,
				_ => throw new InvalidOperationException(),
			};
		}
	}

	public static IEnumerable<CardValue> GetAllValues(IEnumerable<int> configs) =>
		GetAllValues(configs.Cast<CardSourceConfiguration>());

	public static IEnumerable<CardValue> GetAllValues(IEnumerable<CardSourceConfiguration> configurations)
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

	public static IEnumerable<int> GetAllValues(int config) =>
		GetAllValues((CardSourceConfiguration) config);

	public static IEnumerable<int> GetAllValues(CardSourceConfiguration config)
	{
		return config switch
		{
			CardSourceConfiguration.FullDeck => Enumerable.Range(1, 52),
			CardSourceConfiguration.FullDeckWithJokers => Enumerable.Range(1, 54),
			CardSourceConfiguration.Suits => EnumerableUtility.Enumerate(ClubValue, DiamondValue, HeartValue, SpadeValue),
			CardSourceConfiguration.Ranks => Enumerable.Range(FirstRankValue, 13),
			_ => throw new NotImplementedException($"Unimplemented card source configuration: {config}"),
		};
	}

	public static CardValue? GetNextValue(CardValue value, IEnumerable<int> configurations) =>
		GetNextValue(value, configurations.Cast<CardSourceConfiguration>());

	public static CardValue? GetNextValue(CardValue value, IEnumerable<CardSourceConfiguration> configurations)
	{
		var configs = configurations.AsReadOnlyList();
		var values = new List<int>(value.Values);
		for (var index = values.Count - 1; index >= 0; index--)
		{
			var (nextValue, isOverflow) = GetNextValue(values[index], configs[index]);
			values[index] = nextValue;
			if (!isOverflow)
				return new CardValue(values);
		}

		return null;
	}

	public static (int Value, bool Overflow) GetNextValue(int value, int config) =>
		GetNextValue(value, (CardSourceConfiguration) config);

	public static (int Value, bool Overflow) GetNextValue(int value, CardSourceConfiguration config)
	{
		return config switch
		{
			CardSourceConfiguration.FullDeck => GetNextInFullDeck(),
			CardSourceConfiguration.FullDeckWithJokers => GetNextInFullDeckWithJokers(),
			CardSourceConfiguration.Suits => GetNextSuit(),
			CardSourceConfiguration.Ranks => GetNextRank(),
			_ => throw new NotImplementedException($"Unimplemented card source configuration: {config}"),
		};

		(int Value, bool Overflow) GetNextInFullDeck() =>
			value >= 52 ? (1, true) : (value + 1, false);

		(int Value, bool Overflow) GetNextInFullDeckWithJokers() =>
			value >= 54 ? (1, true) : (value + 1, false);

		(int Value, bool Overflow) GetNextSuit() =>
			value >= SpadeValue ? (ClubValue, true) : (value + 1, false);

		(int Value, bool Overflow) GetNextRank() =>
			value >= LastRankValue ? (FirstRankValue, true) : (value + 1, false);
	}

	public static CardValue GetFirstValue(IEnumerable<int> configs) =>
		GetFirstValue(configs.Cast<CardSourceConfiguration>());

	public static CardValue GetFirstValue(IEnumerable<CardSourceConfiguration> configs)
	{
		var values = configs.Select(config =>
		{
			return config switch
			{
				CardSourceConfiguration.FullDeck => 1,
				CardSourceConfiguration.FullDeckWithJokers => 1,
				CardSourceConfiguration.Suits => ClubValue,
				CardSourceConfiguration.Ranks => FirstRankValue,
				_ => throw new NotImplementedException($"Unimplemented card source configuration: {config}"),
			};
		}).AsReadOnlyList();
		return new CardValue(values);
	}

	public static CardValue GetLastValue(IReadOnlyList<int> configs) =>
		GetLastValue(configs.Cast<CardSourceConfiguration>());

	public static CardValue GetLastValue(IEnumerable<CardSourceConfiguration> configs)
	{
		var values = configs.Select(config =>
		{
			return config switch
			{
				CardSourceConfiguration.FullDeck => 52,
				CardSourceConfiguration.FullDeckWithJokers => 54,
				CardSourceConfiguration.Suits => SpadeValue,
				CardSourceConfiguration.Ranks => LastRankValue,
				_ => throw new NotImplementedException($"Unimplemented card source configuration: {config}"),
			};
		}).AsReadOnlyList();
		return new CardValue(values);
	}

	public static (int Suit, int Rank) ConvertCardToSuitAndRank(int card)
	{
		var suit = card switch
		{
			>= 1 and <= 13 => ClubValue,
			>= 14 and <= 26 => DiamondValue,
			>= 27 and <= 39 => HeartValue,
			>= 40 and <= 52 => SpadeValue,
			_ => throw new InvalidOperationException($"Invalid card value: {card}"),
		};
		var rank = (card - 1) % 13 + 1;
		return (suit, rank);
	}

	public static int ConvertSuitAndRankToCard(int suit, int rank) =>
		(suit - 1) * 13 + rank;

	public static IReadOnlyList<int>? MergeConfigurations(IEnumerable<IReadOnlyList<int>> configs) =>
		RandomPlanUtility.MergeConfigurations(configs, MergeConfigurations);

	public static int? MergeConfigurations(int config1, int config2) =>
		(int?) MergeConfigurations((CardSourceConfiguration) config1, (CardSourceConfiguration) config2);

	public static CardSourceConfiguration? MergeConfigurations(CardSourceConfiguration config1, CardSourceConfiguration config2)
	{
		if (config1 == config2)
			return config1;

		if (config1 == CardSourceConfiguration.FullDeck && config2 == CardSourceConfiguration.FullDeckWithJokers)
			return CardSourceConfiguration.FullDeckWithJokers;

		if (config1 == CardSourceConfiguration.FullDeckWithJokers && config2 == CardSourceConfiguration.FullDeck)
			return CardSourceConfiguration.FullDeckWithJokers;

		return null;
	}

	public static (int? Value, int? Config) TryParseSingleValue(string input, CardSourceConfiguration? config)
	{
		int? value;
		CardSourceConfiguration? guessedConfig = null;

		if (config is not null)
		{
			value = config switch
			{
				CardSourceConfiguration.FullDeck => TryParseCard(input),
				CardSourceConfiguration.FullDeckWithJokers => TryParseCard(input),
				CardSourceConfiguration.Suits => TryParseSuit(input),
				CardSourceConfiguration.Ranks => TryParseRank(input),
				_ => throw new NotImplementedException($"Unimplemented card source configuration: {config}"),
			};
			if (value is not null && config == CardSourceConfiguration.FullDeck && IsJoker(value.Value))
				value = null;
			if (value is not null)
				guessedConfig = config;
		}
		else
		{
			value = TryParseCard(input);
			if (value is not null)
				guessedConfig = IsJoker(value.Value) ? CardSourceConfiguration.FullDeckWithJokers : CardSourceConfiguration.FullDeck;
			if (value is null)
			{
				value = TryParseSuit(input);
				if (value is not null)
					guessedConfig = CardSourceConfiguration.Suits;
			}
			if (value is null)
			{
				value = TryParseRank(input);
				if (value is not null)
					guessedConfig = CardSourceConfiguration.Ranks;
			}
		}

		return (value, (int?) guessedConfig);
	}

	public static string GetShortText(int value)
	{
		if (value == BlackJokerValue)
			return OurResources.CardValueBlackJokerShort;
		if (value == RedJokerValue)
			return OurResources.CardValueRedJokerShort;
		if (IsSuit(value))
			return GetSuitShortText(value);
		if (IsRank(value))
			return GetRankShortText(value);
		if (value >= 1 && value <= 52)
		{
			var (suit, rank) = ConvertCardToSuitAndRank(value);
			string formatString = suit switch
			{
				ClubValue => OurResources.CardValueClubShort,
				DiamondValue => OurResources.CardValueDiamondShort,
				HeartValue => OurResources.CardValueHeartShort,
				SpadeValue => OurResources.CardValueSpadeShort,
				_ => throw new InvalidOperationException($"Invalid card value: {value}"),
			};
			var rankString = GetRankShortText(rank);
			return string.Format(formatString, rankString);
		}

		throw new NotImplementedException($"Can not render card value: {value}");
	}

	public static string GetDisplayText(int value)
	{
		if (value == BlackJokerValue)
			return OurResources.CardValueBlackJokerDisplay;
		if (value == RedJokerValue)
			return OurResources.CardValueRedJokerDisplay;
		if (IsSuit(value))
			return GetSuitDisplayText(value);
		if (IsRank(value))
			return GetRankDisplayText(value);
		if (value >= 1 && value <= 52)
		{
			var (suit, rank) = ConvertCardToSuitAndRank(value);
			string formatString = suit switch
			{
				ClubValue => OurResources.CardValueClubDisplay,
				DiamondValue => OurResources.CardValueDiamondDisplay,
				HeartValue => OurResources.CardValueHeartDisplay,
				SpadeValue => OurResources.CardValueSpadeDisplay,
				_ => throw new InvalidOperationException($"Invalid card value: {value}"),
			};
			var rankString = GetRankDisplayText(rank);
			return string.Format(formatString, rankString);
		}

		throw new NotImplementedException($"Can not render card value: {value}");
	}

	public static int? TryParseCard(string input) =>
		s_cardTextToValue.Value.TryGetValue(input, out var value) ? value : null;

	public static int? TryParseSuit(string input) =>
		s_suitTextToValue.Value.TryGetValue(input, out var value) ? value : null;

	public static int? TryParseRank(string input) =>
		s_rankTextToValue.Value.TryGetValue(input, out var value) ? value : null;

	static CardUtility()
	{
		s_cardTextToValue = new Lazy<Dictionary<string, int>>(() =>
		{
			var textToValue = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
			for (var i = 1; i <= 54; i++)
			{
				textToValue.Add(GetShortText(i), i);
				textToValue.Add(GetDisplayText(i), i);
			}
			return textToValue;
		});

		s_suitTextToValue = new Lazy<Dictionary<string, int>>(() =>
		{
			return new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase)
			{
				{ GetSuitShortText(ClubValue), ClubValue },
				{ GetSuitDisplayText(ClubValue), ClubValue },
				{ GetSuitShortText(DiamondValue), DiamondValue },
				{ GetSuitDisplayText(DiamondValue), DiamondValue },
				{ GetSuitShortText(HeartValue), HeartValue },
				{ GetSuitDisplayText(HeartValue), HeartValue },
				{ GetSuitShortText(SpadeValue), SpadeValue },
				{ GetSuitDisplayText(SpadeValue), SpadeValue },
			};
		});

		s_rankTextToValue = new Lazy<Dictionary<string, int>>(() =>
		{
			var textToValue = new Dictionary<string, int>(StringComparer.CurrentCultureIgnoreCase);
			for (var i = FirstRankValue; i <= LastRankValue; i++)
			{
				textToValue.Add(GetRankShortText(i), i);
				textToValue.TryAdd(GetRankDisplayText(i), i);
			}
			return textToValue;
		});
	}

	private static string GetSuitShortText(int value)
	{
		return value switch
		{
			ClubValue => OurResources.CardSuitClubShort,
			DiamondValue => OurResources.CardSuitDiamondShort,
			HeartValue => OurResources.CardSuitHeartShort,
			SpadeValue => OurResources.CardSuitSpadeShort,
			_ => throw new InvalidOperationException($"Invalid suit value: {value}"),
		};
	}

	private static string GetSuitDisplayText(int value)
	{
		return value switch
		{
			ClubValue => OurResources.CardSuitClubDisplay,
			DiamondValue => OurResources.CardSuitDiamondDisplay,
			HeartValue => OurResources.CardSuitHeartDisplay,
			SpadeValue => OurResources.CardSuitSpadeDisplay,
			_ => throw new InvalidOperationException($"Invalid suit value: {value}"),
		};
	}

	private static string GetRankShortText(int value)
	{
		var rank = (value - FirstRankValue) + 1;
		return rank switch
		{
			1 => OurResources.CardValueAceShort,
			11 => OurResources.CardValueJackShort,
			12 => OurResources.CardValueQueenShort,
			13 => OurResources.CardValueKingShort,
			_ => rank.ToString(),
		};
	}

	private static string GetRankDisplayText(int value)
	{
		var rank = (value - FirstRankValue) + 1;
		return rank switch
		{
			1 => OurResources.CardValueAceDisplay,
			11 => OurResources.CardValueJackDisplay,
			12 => OurResources.CardValueQueenDisplay,
			13 => OurResources.CardValueKingDisplay,
			_ => rank.ToString(),
		};
	}

	private static readonly Lazy<Dictionary<string, int>> s_cardTextToValue;
	private static readonly Lazy<Dictionary<string, int>> s_suitTextToValue;
	private static readonly Lazy<Dictionary<string, int>> s_rankTextToValue;
}
