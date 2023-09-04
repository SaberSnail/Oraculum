using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Windows.Async;
using Microsoft.VisualStudio.Threading;
using Oraculum.Engine;

namespace Oraculum.Data
{
	public static class DataImportUtility
	{
		public static async Task<IReadOnlyList<(TableMetadata Metadata, IReadOnlyList<RowData> Rows)>> ImportTablesAsync(TaskStateController state, string path)
		{
			await state.ToThreadPool();

			var tables = new List<(TableMetadata Metadata, IReadOnlyList<RowData> Rows)>();

			var titlesInUse = await AppModel.Instance.Data.GetAllTableTitlesAsync(state.CancellationToken).ConfigureAwait(false);

			var defaultTitle = CreateBestTitleFromFileName(titlesInUse, Path.GetFileNameWithoutExtension(path));

			var lines = await File.ReadAllLinesAsync(path, state.CancellationToken).ConfigureAwait(false);

			List<(int? Number1, int? Number2, string Output, Guid? Next)>? currentRowInfos = null;
			string currentTitle = defaultTitle;
			string? currentSource = null;
			string? currentAuthor = null;
			Guid? currentId = null;
			IReadOnlyList<string> currentGroups = Array.Empty<string>();
			IReadOnlyList<RandomSourceData> currentRandomPlan = Array.Empty<RandomSourceData>();

			var isAllWhitespaceRegex = new Regex(@"^\s*$");
			var parseState = ImportParseState.ReadingMetadata;
			foreach (var line in lines)
			{
				if (line.Length == 0 || isAllWhitespaceRegex.IsMatch(line))
					continue;

				if (line.StartsWith("# "))
				{
					if (parseState == ImportParseState.ReadingRows)
					{
						CreateTableMetadata(currentId, currentTitle, currentAuthor, currentSource, currentRandomPlan, currentGroups, currentRowInfos!);
						currentGroups = Array.Empty<string>();
						currentRandomPlan = Array.Empty<RandomSourceData>();
						currentSource = null;
						currentAuthor = null;
						currentId = null;
						currentRowInfos = null;
						parseState = ImportParseState.ReadingMetadata;
					}
					currentTitle = line[2..].Trim();
				}
				else
				{
					if (parseState == ImportParseState.ReadingMetadata)
					{
						if (line.StartsWith("Groups:", StringComparison.OrdinalIgnoreCase))
						{
							currentGroups = line[7..]
								.Split(" > ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
								.ToList();
						}
						else if (line.StartsWith("Source:", StringComparison.OrdinalIgnoreCase))
						{
							currentSource = line[7..].Trim();
						}
						else if (line.StartsWith("Author:", StringComparison.OrdinalIgnoreCase))
						{
							currentAuthor = line[7..].Trim();
						}
						else if (line.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
						{
							currentId = Guid.Parse(line[3..].Trim());
						}
						else if (line.StartsWith("Random:", StringComparison.OrdinalIgnoreCase))
						{
							currentRandomPlan = ParseRandomPlan(line[7..].Trim());
						}
						else
						{
							currentRowInfos = new List<(int? Number1, int? Number2, string Output, Guid? Next)>();
							parseState = ImportParseState.ReadingRows;
						}
					}

					if (parseState == ImportParseState.ReadingRows)
						currentRowInfos!.Add(CreateRowInfo(line));
				}
			}
			if (currentRowInfos is not null)
				CreateTableMetadata(currentId, currentTitle, currentAuthor, currentSource, currentRandomPlan, currentGroups, currentRowInfos);

			void CreateTableMetadata(Guid? id, string title, string? author, string? source, IReadOnlyList<RandomSourceData> randomPlan, IReadOnlyList<string> groups, List<(int? Number1, int? Number2, string Output, Guid? Next)> rowInfos)
			{
				var rowType = GuessRowType(rowInfos);

				var (randomSource, rows) = rowType switch
				{
					RowKind.NoNumbers => CreateNoNumbersRowDatas(rowInfos),
					RowKind.MixedRanges => CreateMixedRangesRowDatas(rowInfos),
					RowKind.Weighted => CreateWeightedRowDatas(rowInfos),
					_ => throw new InvalidOperationException("Unknown row kind"),
				};

				if (randomPlan.Count == 0)
					randomPlan = new[] { randomSource };
				else if (randomPlan[0] != randomSource)
					throw new FormatException("Calculated random source does not match the expected random plan.");

				var metadata = new TableMetadata
				{
					Id = id ?? Guid.NewGuid(),
					Title = title,
					Source = source,
					Author = author,
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					RandomPlan = randomPlan,
					Groups = groups,
				};

				tables.Add((metadata, rows));
			}

			return tables;
		}

		private static string CreateBestTitleFromFileName(HashSet<string> tableTitles, string fileName)
		{
			var title = StringUtility.GetWordsFromCamelCase(fileName).Join(" ");
			return TitleUtility.GetUniqueTitle(title, tableTitles);
		}

		private static IReadOnlyList<RandomSourceData> ParseRandomPlan(string input)
		{
			var parts = input.Split(";", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
			var regex = new Regex(@"(\d+)d(\d+)");
			return parts
				.Select(text =>
				{
					var match = regex.Match(text);
					if (!match.Success)
						throw new FormatException("Random plan input does not have the expected format.");
					var count = int.Parse(match.Groups[1].Value);
					var sides = int.Parse(match.Groups[2].Value);
					return new RandomSourceData(RandomSourceKind.Die, Enumerable.Range(1, count).Select(x => sides).AsReadOnlyList());
				})
				.AsReadOnlyList();
		}

		private static (int? Number1, int? Number2, string Output, Guid? next) CreateRowInfo(string line)
		{
			var regex = new Regex(@"^(?>\s*(\d+)\s+)?(?>(\d+)\s+)?\.?\s*(.*)$");
			var match = regex.Match(line);
			var number1 = match.Groups[1].Success ? StringUtility.TryParse<int>(match.Groups[1].Value) : null;
			var number2 = match.Groups[2].Success ? StringUtility.TryParse<int>(match.Groups[2].Value) : null;
			if (number1 == 0)
				number1 = 100;
			if (number2 == 0)
				number2 = 100;
			var output = match.Groups[3].Value;
			var nextRegex = new Regex(@"(\s*\[(.*)\])?$");
			var nextMatch = nextRegex.Match(output);
			Guid? nextId = null;
			if (nextMatch.Success && Guid.TryParse(nextMatch.Groups[2].Value, out var next))
			{
				nextId = next;
				output = output.Substring(0, nextMatch.Groups[1].Index);
			}

			return (number1, number2, output, nextId);
		}

		private static RowKind GuessRowType(IReadOnlyList<(int? Number1, int? Number2, string Output, Guid? Next)> rowInfos)
		{
			if (rowInfos.All(x => x.Number1 is null && x.Number2 is null))
				return RowKind.NoNumbers;

			if (rowInfos.All(x => x.Number1 is not null && x.Number2 is null))
			{
				bool isSequential = true;
				for (int i = 0; i < rowInfos.Count - 1; i++)
				{
					if (rowInfos[i].Number1 != i + 1)
					{
						isSequential = false;
						break;
					}
				}
				if (!isSequential)
					return RowKind.Weighted;
			}

			if (rowInfos.All(x => x.Number1 is not null))
				return RowKind.MixedRanges;

			return RowKind.Unknown;
		}

		private static (RandomSourceData RandomSource, IReadOnlyList<RowData> Rows) CreateNoNumbersRowDatas(IReadOnlyList<(int? Number1, int? Number2, string Output, Guid? Next)> rowInfos)
		{
			var dice = GetBestDice(rowInfos.Count);
			if (dice.Count != 1)
				throw new NotImplementedException("Multiple dice not supported yet.");

			var increment = dice.Count == 1 && dice[0] % rowInfos.Count == 0 ? dice[0] / rowInfos.Count : 1;
			var randomSource = new RandomSourceData
			{
				Kind = RandomSourceKind.Die,
				Dice = dice,
			};

			int startValue = 1;
			var rows = rowInfos
				.Select((info, index) =>
				{
					var data = new RowData
					{
						Min = startValue,
						Max = startValue + increment - 1,
						Output = info.Output,
						Next = info.Next,
					};
					startValue += increment;
					return data;
				})
				.AsReadOnlyList();

			return (randomSource, rows);
		}

		private static (RandomSourceData RandomSource, IReadOnlyList<RowData> Rows) CreateMixedRangesRowDatas(IReadOnlyList<(int? Number1, int? Number2, string Output, Guid? Next)> rowInfos)
		{
			var randomSource = new RandomSourceData
			{
				Kind = RandomSourceKind.Die,
				Dice = new[] { (rowInfos[^1].Number2 ?? rowInfos[^1].Number1)!.Value },
			};

			int lastMax = 0;
			var rows = rowInfos
				.Select((info, index) =>
				{
					if (info.Number1!.Value != lastMax + 1)
						throw new InvalidOperationException("Dice values must be sequential.");
					lastMax = (info.Number2 ?? info.Number1)!.Value;

					var data = new RowData
					{
						Min = info.Number1!.Value,
						Max = lastMax,
						Output = info.Output,
						Next = info.Next,
					};
					return data;
				})
				.AsReadOnlyList();

			return (randomSource, rows);
		}

		private static (RandomSourceData RandomSource, IReadOnlyList<RowData> Rows) CreateWeightedRowDatas(IReadOnlyList<(int? Number1, int? Number2, string Output, Guid? Next)> rowInfos)
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
					var data = new RowData
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

		private enum RowKind
		{
			Unknown,
			NoNumbers,
			MixedRanges,
			Weighted,
		};
	}
}