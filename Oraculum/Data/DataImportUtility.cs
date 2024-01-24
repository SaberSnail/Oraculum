using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.Engine;

namespace Oraculum.Data
{
	public static class DataImportUtility
	{
		public static async Task<IReadOnlyList<(TableMetadataDto Metadata, IReadOnlyList<RowDataDto> Rows)>> ImportTablesAsync(TaskStateController state, string filePath)
		{
			await state.ToSyncContext();

			var titlesInUse = AppModel.Instance.Data.GetAllTableTitles();

			await state.ToThreadPool();

			using var logScope = Log.TimedInfo($"Importing tables from \"{filePath}\"...");

			string defaultTitle = CreateBestTitleFromFileName(titlesInUse, Path.GetFileNameWithoutExtension(filePath));

			string[]? lines = null;
			try
			{
				lines = await File.ReadAllLinesAsync(filePath, state.CancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException or ArgumentException)
			{
				Log.Info($"Error reading file: {ex}");
				return new List<(TableMetadataDto Metadata, IReadOnlyList<RowDataDto> Rows)>();
			}

			try
			{
				return ParseLines(lines, defaultTitle);
			}
			catch (FormatException ex)
			{
				Log.Info($"Error parsing file: {ex}");
				return new List<(TableMetadataDto Metadata, IReadOnlyList<RowDataDto> Rows)>();
			}
		}

		private static IReadOnlyList<(TableMetadataDto Metadata, IReadOnlyList<RowDataDto> Rows)> ParseLines(IReadOnlyList<string> lines, string defaultTitle)
		{
			var tables = new List<(TableMetadataDto Metadata, IReadOnlyList<RowDataDto> Rows)>();

			Guid? currentId = null;
			var currentTitle = defaultTitle;
			string? currentSource = null;
			string? currentAuthor = null;
			var currentVersion = 1;
			var currentCreated = DateOnly.FromDateTime(DateTime.Now);
			var currentModified = currentCreated;
			string? currentDescription = null;
			RandomPlan? currentRandomPlan = null;
			RandomSourceBase? currentRandomSource = null;
			IReadOnlyList<string> currentGroups = Array.Empty<string>();
			List<(RandomValueBase Value1, RandomValueBase? Value2, IReadOnlyList<int>? GuessedConfigs, string Output)>? currentRowInfos = null;

			var parseState = ImportParseState.ReadingMetadata;
			foreach (var line in lines.Select(x => x.Trim()))
			{
				if (line.Length == 0)
					continue;

				if (line.StartsWith("# "))
				{
					if (parseState == ImportParseState.ReadingRows)
					{
						tables.Add(CreateTable(currentId, currentTitle, currentSource, currentAuthor, currentVersion, currentCreated, currentModified, currentDescription, currentRandomPlan, currentGroups, currentRowInfos));
						currentId = null;
						currentSource = null;
						currentAuthor = null;
						currentVersion = 1;
						currentCreated = DateOnly.FromDateTime(DateTime.Now);
						currentModified = currentCreated;
						currentDescription = null;
						currentRandomPlan = null;
						currentRandomSource = null;
						currentGroups = Array.Empty<string>();
						currentRowInfos = null;
						parseState = ImportParseState.ReadingMetadata;
					}
					currentTitle = line[2..];
				}
				else
				{
					if (parseState == ImportParseState.ReadingMetadata)
					{
						if (line.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
						{
							currentId = Guid.Parse(line[3..]);
						}
						else if (line.StartsWith("Source:", StringComparison.OrdinalIgnoreCase))
						{
							currentSource = line[7..];
						}
						else if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
						{
							currentAuthor = line[7..];
						}
						else if (line.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
						{
							if (int.TryParse(line[8..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var version))
								currentVersion = version;
							else
								throw new FormatException($"Version must be an integer: {line}");
						}
						else if (line.StartsWith("Created:", StringComparison.OrdinalIgnoreCase))
						{
							var dateValue = line[8..].Trim();
							var yearRegex = new Regex(@"^\d{4}$");
							if (yearRegex.IsMatch(dateValue))
								currentCreated = new DateOnly(int.Parse(dateValue), 1, 1);
							else if (DateOnly.TryParse(line[8..], CultureInfo.InvariantCulture, out var created))
								currentCreated = created;
							else
								throw new FormatException($"Created date could not be parsed: {line}");
						}
						else if (line.StartsWith("Modified:", StringComparison.OrdinalIgnoreCase))
						{
							if (DateOnly.TryParse(line[9..], CultureInfo.InvariantCulture, out var modified))
								currentModified = modified;
							else
								throw new FormatException($"Updated date could not be parsed: {line}");
						}
						else if (line.StartsWith("Description:", StringComparison.OrdinalIgnoreCase))
						{
							currentDescription = line[12..];
						}
						else if (line.StartsWith("Random:", StringComparison.OrdinalIgnoreCase))
						{
							currentRandomPlan = ParseRandomPlan(line[7..]);
							currentRandomSource = RandomSourceBase.Create(currentRandomPlan);
						}
						else if (line.StartsWith("Groups:", StringComparison.OrdinalIgnoreCase))
						{
							currentGroups = line[7..]
								.Split(">", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
								.ToList();
						}
						else
						{
							currentRowInfos = new List<(RandomValueBase Value1, RandomValueBase? Value2, IReadOnlyList<int>? GuessedConfigs, string Output)>();
							parseState = ImportParseState.ReadingRows;
						}
					}

					if (parseState == ImportParseState.ReadingRows)
						currentRowInfos!.Add(CreateRowInfo(line, currentRandomSource));
				}
			}
			if (currentTitle is not null)
				tables.Add(CreateTable(currentId, currentTitle, currentSource, currentAuthor, currentVersion, currentCreated, currentModified, currentDescription, currentRandomPlan, currentGroups, currentRowInfos));

			return tables;
		}

		private static (TableMetadataDto Metadata, IReadOnlyList<RowDataDto> Rows) CreateTable(Guid? id, string title, string? source, string? author, int version, DateOnly created, DateOnly modified, string? description, RandomPlan? randomPlan, IReadOnlyList<string> groups, IReadOnlyList<(RandomValueBase Value1, RandomValueBase? Value2, IReadOnlyList<int>? GuessedConfigs, string Output)>? rowInfos)
		{
			if (rowInfos is null)
				throw new FormatException($"Table is missing rows: {title}");

			var transformedRows = rowInfos;

			if (randomPlan is null)
				randomPlan = GuessRandomPlan(rowInfos);

			if (rowInfos[0].GuessedConfigs is null)
			{
				transformedRows = ApplyRowWeights().AsReadOnlyList();

				IEnumerable<(RandomValueBase Value1, RandomValueBase? Value2, IReadOnlyList<int>? GuessedConfigs, string Output)> ApplyRowWeights()
				{
					var currentWeight = 0;
					foreach (var rowInfo in rowInfos)
					{
						currentWeight += rowInfo.Value1.Values[0];
						yield return (new DieValue(currentWeight), null, null, rowInfo.Output);
					}
				}
			}
			else if (rowInfos[0].Value1.Values.Count == 1 && randomPlan.Configurations.Count > 1 && randomPlan.Kind == RandomSourceKind.DiceSequence)
			{
				transformedRows = ApplySequenceTransform().AsReadOnlyList();

				IEnumerable<(RandomValueBase Value1, RandomValueBase? Value2, IReadOnlyList<int>? GuessedConfigs, string Output)> ApplySequenceTransform()
				{
					foreach (var rowInfo in rowInfos)
					{
						var value1 = new DieValue(DigitCollection.Create(rowInfo.Value1.Values[0])
							.Select(x => x)
							.AsReadOnlyList());
						RandomValueBase? value2 = null;
						if (rowInfo.Value2 is not null)
						{
							value2 = new DieValue(DigitCollection.Create(rowInfo.Value2.Values[0])
								.Select(x => x)
								.AsReadOnlyList());
						}
						yield return (value1, value2, rowInfo.GuessedConfigs, rowInfo.Output);
					}
				}
			}

			ValidateRows(transformedRows, randomPlan);
			var rows = CreateRows(transformedRows);

			var metadata = new TableMetadataDto(
				tableId: id ?? Guid.NewGuid(),
				title: title,
				source: source,
				author: author,
				version: 1,
				created: created,
				modified: modified,
				description: description,
				randomPlan: randomPlan!,
				groups: groups
			);

			return (metadata, rows);
		}

		private static void ValidateRows(IReadOnlyList<(RandomValueBase Value1, RandomValueBase? Value2, IReadOnlyList<int>? GuessedConfigs, string Output)> rows, RandomPlan randomPlan)
		{
			var values = new HashSet<RandomValueBase>();
			foreach (var row in rows)
			{
				if (row.Value2 is null)
				{
					if (!values.Add(row.Value1))
						throw new FormatException($"Duplicate row value: {row.Value1}");
				}
				else
				{
					foreach (var value in row.Value1.EnumerateTo(row.Value2, randomPlan.Configurations))
					{
						if (!values.Add(value))
							throw new FormatException($"Duplicate row value: {value}");
					}
				}
			}
		}

		private static RandomPlan GuessRandomPlan(IReadOnlyList<(RandomValueBase Value1, RandomValueBase? Value2, IReadOnlyList<int>? GuessedConfigs, string Output)> rowInfos)
		{
			var valueKind = rowInfos[0].Value1.Kind;
			if (rowInfos.Any(x => (x.Value1?.Kind ?? RandomValueKind.Die) != valueKind))
				throw new FormatException("All Row values must be the same kind.");

			RandomSourceKind sourceKind;
			IReadOnlyList<int>? configs;
			var configCount = rowInfos[0].GuessedConfigs?.Count;
			if (configCount is null)
			{
				if (rowInfos.Any(x => x.GuessedConfigs is not null))
					throw new FormatException("All Row values must have the same number of configurations.");
				sourceKind = RandomSourceKind.DiceSequence;
				configs = new[] { rowInfos.Sum(x => x.Value1.Values[0]) };
			}
			else
			{
				if (configCount is not null && rowInfos.Any(x => x.GuessedConfigs?.Count != configCount))
					throw new FormatException("All Row values must have the same number of configurations.");

				configs = valueKind switch
				{
					RandomValueKind.Card => CardUtility.MergeConfigurations(rowInfos.Select(x => x.GuessedConfigs!)),
					RandomValueKind.Die => DieUtility.MergeConfigurations(rowInfos.Select(x => x.GuessedConfigs!)),
					_ => throw new NotImplementedException("Unknown random value kind"),
				};
				if (configs is null)
					throw new FormatException("Row configurations are not compatible.");

				sourceKind = valueKind == RandomValueKind.Die ? RandomSourceKind.DiceSequence : RandomSourceKind.CardSequence;
				var minValue = rowInfos.Min(x => x.Value1.Values[0]);
				if (sourceKind == RandomSourceKind.DiceSequence && configs.Count == 1 && minValue > 1)
				{
					var maxValue = rowInfos.Max(x => (x.Value2 ?? x.Value1).Values[0]);
					DigitCollection minDigits = minValue;
					DigitCollection maxDigits = maxValue;

					if (minDigits.Count == maxDigits.Count && minDigits.Count > 1)
					{
						var hasValidMinValue = minDigits.All(x => x == 1);
						var hasValidMaxValue = maxDigits.All(x => x == maxDigits[0]);
						if (hasValidMinValue && hasValidMaxValue)
							return new RandomPlan(sourceKind, maxDigits.ToArray());
					}
					else if (maxValue % minValue == 0)
					{
						return new RandomPlan(RandomSourceKind.DiceSum, Enumerable.Repeat(maxValue / minValue, minValue).ToArray());
					}

					throw new FormatException("Unable to guess random plan from rows.");
				}
			}

			return new RandomPlan(sourceKind, configs);
		}

		private static IReadOnlyList<RowDataDto> CreateRows(IEnumerable<(RandomValueBase Value1, RandomValueBase? Value2, IReadOnlyList<int>? GuessedConfigs, string Output)> rowInfos)
		{
			return rowInfos
				.Select(x => new RowDataDto(x.Value1.Values, (x.Value2 ?? x.Value1).Values, x.Output))
				.AsReadOnlyList();
		}

		private static string CreateBestTitleFromFileName(HashSet<string> tableTitles, string fileName)
		{
			string title = StringUtility.GetWordsFromCamelCase(fileName).Join(" ").Replace('_', ' ');
			return TitleUtility.GetUniqueTitle(title, tableTitles);
		}

		private static RandomPlan ParseRandomPlan(string input)
		{
			var kind = RandomSourceKind.DiceSequence;

			var isSum = input.Contains("+");
			var parts = input.Split(isSum ? "+" : ";", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

			var diceConfigRegex = new Regex(@"(\d*)d(\d+)");
			var cardConfigRegex = new Regex($"({EnumUtility.Values<CardSourceConfiguration>().Select(x => x.ToString()).Join("|")})");
			if (parts.All(x => diceConfigRegex.IsMatch(x)))
				kind = isSum ? RandomSourceKind.DiceSum : RandomSourceKind.DiceSequence;
			else if (parts.All(x => cardConfigRegex.IsMatch(x)) && !isSum)
				kind = RandomSourceKind.CardSequence;
			else
				throw new FormatException($"Random does not have the expected format: {input}");

			var configurations = parts
				.SelectMany(part =>
				{
					if (kind == RandomSourceKind.CardSequence)
					{
						var match = cardConfigRegex.Match(part);
						if (!match.Success)
							throw new FormatException($"Random does not have the expected format: {input}");
						if (!Enum.TryParse<CardSourceConfiguration>(match.Groups[1].Value, out var config))
							throw new FormatException($"Random does not have the expected format: {input}");
						return EnumerableUtility.Enumerate((int) config);
					}
					else
					{
						var match = diceConfigRegex.Match(part);
						if (!match.Success)
							throw new FormatException($"Random does not have the expected format: {input}");
						var countValue = match.Groups[1].Value;
						var count = countValue.Length == 0 ? 1 : int.Parse(countValue);
						var sides = int.Parse(match.Groups[2].Value);
						return Enumerable.Repeat(sides, count);
					}
				})
				.AsReadOnlyList();

			return new RandomPlan(kind, configurations);
		}

		private static (RandomValueBase Value1, RandomValueBase? Value2, IReadOnlyList<int>? GuessedConfigs, string Output) CreateRowInfo(string line, RandomSourceBase? randomSource)
		{
			var regex = new Regex(@"^(?:((?:\d+,\s*)+\d+|\d+\s*-\s*\d+|\d+)(?:\s*\.\s+|\s*\:\s+|\s+))?(.*)$");
			var match = regex.Match(line);
			var value = match.Groups[1].Success ? match.Groups[1].Value : "";

			if (!match.Groups[2].Success)
				throw new FormatException($"Row output is missing: {line}");

			var valueParts = value.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			if (valueParts.Length > 2)
				throw new FormatException($"Row value has more than one range: {line}");

			RandomValueBase? value1 = null;
			RandomValueBase? value2 = null;
			IReadOnlyList<int>? configs = null;
			if (randomSource is not null)
			{
				if (valueParts.Length > 0)
					value1 = randomSource.TryConvertToValue(valueParts[0]);
				if (valueParts.Length > 1)
					value2 = randomSource.TryConvertToValue(valueParts[1]);
			}
			else
			{
				if (valueParts.Length > 0)
				{
					(value1, configs) = GuessValue(valueParts[0]);
					if (value1 is null)
						throw new FormatException($"First row value could not be parsed: {line}");
				}
				if (valueParts.Length > 1)
				{
					(value2, var checkConfigs) = GuessValue(valueParts[1]);
					if (value2 is null)
						throw new FormatException($"Second row value could not be parsed: {line}");
					if (value1!.Kind != value2.Kind)
						throw new FormatException($"Row values must be the same kind: {line}");
					if (value1!.Values.Count != value2.Values.Count)
						throw new FormatException($"Row values must have the same number of values: {line}");
					configs = configs!
						.Zip(checkConfigs)
						.Select(x =>
						{
							var (config1, config2) = x;
							var mergedConfig = value1.Kind == RandomValueKind.Die ?
								DieUtility.MergeConfigurations(config1, config2) :
								(int?) CardUtility.MergeConfigurations((CardSourceConfiguration) config1, (CardSourceConfiguration) config2);
							if (mergedConfig is null)
								throw new FormatException($"Row values must have the same configuration: {line}");
							return mergedConfig.Value;
						})
						.AsReadOnlyList();
				}
				if (valueParts.Length > 1 && (value1?.Values.Count > 1 || value2?.Values.Count > 1))
					throw new FormatException($"Row value doesn't support a range and a list: {line}");
			}
			if (value1 is null)
				value1 = new DieValue(1);

			return (value1, value2, configs, match.Groups[2].Value);
		}

		private static (RandomValueBase Value, IReadOnlyList<int> Configs) GuessValue(string input)
		{
			var tokens = input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

			var values = new List<int>();
			var configs = new List<int>();
			RandomValueKind? kind = null;

			for (int index = 0; index < tokens.Length; index++)
			{
				var token = tokens[index];
				var (value, config) = RandomValueBase.TryParseSingleValue(token);
				if (value is null)
					throw new FormatException($"Value could not be parsed: {input}");
				if (kind is null)
					kind = value.Kind;
				else if (kind != value.Kind)
					throw new FormatException($"Value kind does not match previous values: {input}");

				values.Add(value.Values[0]);
				configs.Add(config!.Value);
			}

			return kind switch
			{
				RandomValueKind.Die => (new DieValue(values), configs),
				RandomValueKind.Card => (new CardValue(values), configs),
				_ => throw new InvalidOperationException("Unknown random value kind"),
			};
		}

		/*
		private static (RandomSourceData RandomSource, IReadOnlyList<RowDataDto> Rows) CreateWeightedRowDatas(IReadOnlyList<(int? Number1, int? Number2, string Output, Guid? Next)> rowInfos)
		{
			var totalWeight = rowInfos.Sum(x => x.Number1!.Value);

			var dice = GetBestDice(totalWeight);
			if (dice.Count != 1)
				throw new NotImplementedException("Multiple dice not supported yet.");

			var increment = dice.Count == 1 && dice[0] % rowInfos.Count == 0 ? dice[0] / totalWeight : 1;
			var randomSource = new RandomSourceData
			{
				Kind = RandomSourceKind.Die,
				Dice = dice,
			};

			int startValue = 1;
			var rows = rowInfos
				.Select((info, index) =>
				{
					var data = new RowDataDto
					{
						Min = startValue,
						Max = startValue + (info.Number1!.Value * increment) - 1,
						Output = info.Output,
						Next = info.Next,
					};
					startValue += (info.Number1!.Value * increment);
					return data;
				})
				.AsReadOnlyList();

			return (randomSource, rows);
		}
		*/

		private static IReadOnlyList<int> GetBestDice(int rowCount)
		{
			int[] sides = { 4, 6, 8, 10, 12, 20, 100 };
			var matchExact = sides.FirstOrDefault(x => x == rowCount, 0);
			if (matchExact != 0)
				return new[] { matchExact };

			var matchMultiple = sides.FirstOrDefault(x => x % rowCount == 0, 0);
			if (matchMultiple != 0)
				return new[] { matchMultiple };

			int[] halfSides = { 2, 3, 5, 50 };
			var crossSides = sides.SelectMany(x => sides.Select(y => x < y ? (x, y) : (y, x)))
				.Concat(sides.SelectMany(x => halfSides.Select(y => x < y ? (x, y) : (y, x))))
				.Concat(halfSides.SelectMany(x => halfSides.Select(y => x < y ? (x, y) : (y, x))))
				.Distinct()
				.AsReadOnlyList();
			var matchCross = crossSides.FirstOrDefault(x => x.Item1 * x.Item2 == rowCount, (0, 0));
			if (matchCross != (0, 0))
				return new[] { matchCross.Item1, matchCross.Item2 };

			return new[] { rowCount };
		}

		private enum ImportParseState
		{
			ReadingMetadata,
			ReadingRows,
		};

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(DataImportUtility));
	}
}