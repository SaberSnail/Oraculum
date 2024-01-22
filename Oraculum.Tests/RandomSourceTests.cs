using FluentAssertions;
using Oraculum.Data;
using Oraculum.Engine;

namespace Oraculum.Tests
{
	public class RandomSourceTests
	{
		[TestCase("", null)]
		[TestCase("20", null)]
		[TestCase("1", null)]
		[TestCase("1 1", null)]
		[TestCase("1 1 1", null)]
		[TestCase("1 1 1 1", null)]
		[TestCase("1 1 1 1 1", null)]
		[TestCase("1, 1, 1, 1, 1, 1", "6")]
		[TestCase("1 1 1 1 1 1", "6")]
		[TestCase("4, 6, 8, 10, 12, 20", "60")]
		[TestCase("4 6 8 10 12 20", "60")]
		[TestCase("5 1 1 1 1 1", null)]
		[TestCase("1 7 1 1 1 1", null)]
		[TestCase("1 1 9 1 1 1", null)]
		[TestCase("1 1 1 11 1 1", null)]
		[TestCase("1 1 1 1 13 1", null)]
		[TestCase("1 1 1 1 1 21", null)]
		[TestCase("0 1 1 1 1 1", null)]
		[TestCase("1 0 1 1 1 1", null)]
		[TestCase("1 1 0 1 1 1", null)]
		[TestCase("1 1 1 0 1 1", null)]
		[TestCase("1 1 1 1 0 1", null)]
		[TestCase("1 1 1 1 1 0", null)]
		[TestCase("1 1 1 1 1 1 1", null)]
		public void ParseAndRenderShortDiceSumTest(string input, string output) =>
			ParseAndRenderShortTest(s_diceSumPlan, input, output);

		[TestCase("", null)]
		[TestCase("20", null)]
		[TestCase("1", null)]
		[TestCase("1 1", null)]
		[TestCase("1 1 1", null)]
		[TestCase("1 1 1 1", null)]
		[TestCase("1 1 1 1", null)]
		[TestCase("1, 1, 1, 1, 1", "1 1 1 1 1")]
		[TestCase("1,1,1,1,1", "1 1 1 1 1")]
		[TestCase("1 1 1 1 1", "1 1 1 1 1")]
		[TestCase("4, 6, 8, 10, 12", "4 6 8 10 12")]
		[TestCase("4 6 8 10 12", "4 6 8 10 12")]
		[TestCase("5 1 1 1 1", null)]
		[TestCase("1 7 1 1 1", null)]
		[TestCase("1 1 9 1 1", null)]
		[TestCase("1 1 1 11 1", null)]
		[TestCase("1 1 1 1 13", null)]
		[TestCase("0 1 1 1 1", null)]
		[TestCase("1 0 1 1 1", null)]
		[TestCase("1 1 0 1 1", null)]
		[TestCase("1 1 1 0 1", null)]
		[TestCase("1 1 1 1 0", null)]
		[TestCase("1 1 1 1 1 1", null)]
		public void ParseAndRenderShortDiceSequenceTest(string input, string output) =>
			ParseAndRenderShortTest(s_diceSequencePlan, input, output);

		[TestCase("", null)]
		[TestCase("1 1 1 1", null)]
		[TestCase("4D BJ H K", "4D BJ H K")]
		[TestCase("4D, BJ, H, K", "4D BJ H K")]
		[TestCase("4D,BJ,H,K", "4D BJ H K")]
		[TestCase("BJ BJ H K", null)]
		[TestCase("4D BJ 4D K", null)]
		[TestCase("4D BJ H 4D", null)]
		[TestCase("4D BJ H K 3S", null)]
		public void ParseAndRenderShortCardSequenceTest(string input, string output) =>
			ParseAndRenderShortTest(s_cardSequencePlan, input, output);

		private static void ParseAndRenderShortTest(RandomPlan plan, string input, string output)
		{
			var source = RandomSourceBase.Create(plan);
			if (output is null)
				source.Should().BeNull();
			else
				source.Should().NotBeNull();

			var value = source?.TryConvertToValue(input);
			if (output is null)
				value.Should().BeNull();
			else
				value.Should().NotBeNull();

			(value?.ShortText).Should().Be(output);
		}

		public void GetDiceSumPossibleValuesTest() =>
			GetPossibleValuesTest(s_diceSumPlan, 4 + 6 + 8 + 10 + 12 + 20 - 6 + 1);

		public void GetDiceSequencePossibleValuesTest() =>
			GetPossibleValuesTest(s_diceSequencePlan, 4 * 6 * 8 * 10 * 12);

		public void GetCardSequencePossibleValuesTest() =>
			GetPossibleValuesTest(s_cardSequencePlan, 52 * 54 * 4 * 13);

		private static void GetPossibleValuesTest(RandomPlan plan, int expectedCount)
		{
			var source = RandomSourceBase.Create(plan);
			source.Should().NotBeNull();

			var possibleValues = source.GetPossibleValues();
			possibleValues.Should().HaveCount(expectedCount);
		}

		private static RandomPlan s_diceSumPlan = new RandomPlan(
			Engine.RandomSourceKind.DiceSum,
			new List<int> { 4, 6, 8, 10, 12, 20 }
		);

		private static RandomPlan s_diceSequencePlan = new RandomPlan(
			Engine.RandomSourceKind.DiceSequence,
			new List<int> { 4, 6, 8, 10, 12 }
		);

		private static RandomPlan s_cardSequencePlan = new RandomPlan(
			Engine.RandomSourceKind.CardSequence,
			new List<int>
			{
				(int) CardSourceConfiguration.FullDeck,
				(int) CardSourceConfiguration.FullDeckWithJokers,
				(int) CardSourceConfiguration.Suits,
				(int) CardSourceConfiguration.Ranks,
			}
		);
	}
}
