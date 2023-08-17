using System;
using System.Collections.Generic;

namespace Oraculum.Engine;

public static class CardUtility
{
	public static string GetShortText(int value)
	{
		if (value == 53)
			return OurResources.CardValueBlackJokerShort;
		else if (value == 54)
			return OurResources.CardValueRedJokerShort;

		string formatString = value switch
		{
			>= 1 and <= 13 => OurResources.CardValueClubShort,
			>= 14 and <= 26 => OurResources.CardValueDiamondShort,
			>= 27 and <= 39 => OurResources.CardValueSpadeShort,
			_ => OurResources.CardValueHeartShort,
		};

		var rank = (value - 1) % 13 + 1;
		string rankString = rank switch
		{
			1 => OurResources.CardValueAceShort,
			11 => OurResources.CardValueJackShort,
			12 => OurResources.CardValueQueenShort,
			13 => OurResources.CardValueKingShort,
			_ => rank.ToString(),
		};

		return string.Format(formatString, rankString);
	}

	public static string GetDisplayText(int value)
	{
		if (value == c_blackJokerValue)
			return OurResources.CardValueBlackJokerDisplay;
		else if (value == c_redJokerValue)
			return OurResources.CardValueRedJokerDisplay;

		string formatString = value switch
		{
			>= 1 and <= 13 => OurResources.CardValueClubDisplay,
			>= 14 and <= 26 => OurResources.CardValueDiamondDisplay,
			>= 27 and <= 39 => OurResources.CardValueSpadeDisplay,
			_ => OurResources.CardValueHeartDisplay,
		};

		var rank = (value - 1) % 13 + 1;
		string rankString = rank switch
		{
			1 => OurResources.CardValueAceDisplay,
			11 => OurResources.CardValueJackDisplay,
			12 => OurResources.CardValueQueenDisplay,
			13 => OurResources.CardValueKingDisplay,
			_ => rank.ToString(),
		};

		return string.Format(formatString, rankString);
	}

	public static int? TryParse(string input) =>
		s_cardTextToValue.Value.TryGetValue(input, out var value) ? value : null;

	public static bool IsJoker(int value) =>
		value == c_blackJokerValue || value == c_redJokerValue;

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
	}

	private const int c_blackJokerValue = 53;
	private const int c_redJokerValue = 54;

	private static Lazy<Dictionary<string, int>> s_cardTextToValue;
}
