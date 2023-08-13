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
	public static class DataManagerUtility
	{
		public static async Task<IReadOnlyList<(TableMetadata Metadata, IReadOnlyList<RowData> Rows)>> CreateTableDatasAsync(TaskStateController state, string path)
		{
			await state.ToThreadPool();

			var tables = new List<(TableMetadata Metadata, IReadOnlyList<RowData> Rows)>();

			var titlesInUse = await AppModel.Instance.Data.GetAllTableTitlesAsync(state.CancellationToken).ConfigureAwait(false);

			var defaultTitle = CreateBestTitleFromFileName(titlesInUse, Path.GetFileNameWithoutExtension(path));
			var author = System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\')[1];

			var lines = await File.ReadAllLinesAsync(path, state.CancellationToken).ConfigureAwait(false);

			var isAllWhitespaceRegex = new Regex(@"^\s*$");
			List<(int? Number1, int? Number2, string Output)>? currentRowInfos = null;
			string currentTitle = defaultTitle;
			List<string>? currentGroups = null;
			var parseState = ImportParseState.ReadingMetadata;
			foreach (var line in lines)
			{
				if (line.Length == 0 || isAllWhitespaceRegex.IsMatch(line))
					continue;

				if (line.StartsWith("# "))
				{
					if (parseState == ImportParseState.ReadingRows)
					{
						CreateTableMetadata(currentTitle, currentGroups, currentRowInfos!);
						currentGroups = null;
						currentRowInfos = null;
						parseState = ImportParseState.ReadingMetadata;
					}
					currentTitle = line[2..].Trim();
				}
				else if (line.StartsWith("## "))
				{
					if (parseState == ImportParseState.ReadingRows)
					{
						CreateTableMetadata(currentTitle, currentGroups, currentRowInfos!);
						currentTitle = defaultTitle;
						currentRowInfos = null;
						parseState = ImportParseState.ReadingMetadata;
					}
					currentGroups = line[3..]
						.Split(" > ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
						.ToList();
				}
				else
				{
					if (parseState == ImportParseState.ReadingMetadata)
					{
						currentRowInfos = new List<(int? Number1, int? Number2, string Output)>();
						parseState = ImportParseState.ReadingRows;
					}
					currentRowInfos!.Add(CreateRowInfo(line));
				}
			}
			if (currentRowInfos is not null)
				CreateTableMetadata(currentTitle, currentGroups, currentRowInfos);

			void CreateTableMetadata(string title, List<string>? groups, List<(int? Number1, int? Number2, string Output)> rowInfos)
			{
				var rowType = GuessRowType(rowInfos);

				var rows = rowType switch
				{
					RowKind.NoNumbers => CreateNoNumbersRowDatas(rowInfos),
					RowKind.MixedRanges => CreateMixedRangesRowDatas(rowInfos),
					RowKind.Weighted => CreateWeightedRowDatas(rowInfos),
					_ => throw new InvalidOperationException("Unknown row kind"),
				};

				var metadata = new TableMetadata
				{
					Id = Guid.NewGuid(),
					Title = title,
					Author = author,
					Version = 1,
					Created = DateTime.Now,
					Modified = DateTime.Now,
					RandomSource = rows.RandomSource,
					Groups = (IReadOnlyList<string>?) groups ?? Array.Empty<string>(),
				};

				tables.Add((metadata, rows.Rows));
			}

			return tables;
		}

		private static string CreateBestTitleFromFileName(HashSet<string> tableTitles, string fileName)
		{
			var title = StringUtility.GetWordsFromCamelCase(fileName).Join(" ");
			return TitleUtility.GetUniqueTitle(title, tableTitles);
		}

		private static (int? Number1, int? Number2, string Output) CreateRowInfo(string line)
		{
			var regex = new Regex(@"^\s*(\d+)?\s*(?>-\s*(\d+))?\s*\.?\s*(.*)$");
			var match = regex.Match(line);
			var number1 = match.Groups[1].Success ? StringUtility.TryParse<int>(match.Groups[1].Value) : null;
			var number2 = match.Groups[2].Success ? StringUtility.TryParse<int>(match.Groups[2].Value) : null;
			if (number1 == 0)
				number1 = 100;
			if (number2 == 0)
				number2 = 100;
			return (number1, number2, match.Groups[3].Value);
		}

		private static RowKind GuessRowType(IReadOnlyList<(int? Number1, int? Number2, string Output)> rowInfos)
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

		private static (RandomSourceData RandomSource, IReadOnlyList<RowData> Rows) CreateNoNumbersRowDatas(IReadOnlyList<(int? Number1, int? Number2, string Output)> rowInfos)
		{
			var dice = GetBestDice(rowInfos.Count);
			if (dice.Count != 1)
				throw new NotImplementedException("Multiple dice not supported yet.");

			var increment = dice.Count == 1 && dice[0] % rowInfos.Count == 0 ? dice[0] / rowInfos.Count : 1;
			var randomSource = new RandomSourceData
			{
				Kind = RandomSourceKind.Dice,
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
					};
					startValue += increment;
					return data;
				})
				.AsReadOnlyList();

			return (randomSource, rows);
		}

		private static (RandomSourceData RandomSource, IReadOnlyList<RowData> Rows) CreateMixedRangesRowDatas(IReadOnlyList<(int? Number1, int? Number2, string Output)> rowInfos)
		{
			var randomSource = new RandomSourceData
			{
				Kind = RandomSourceKind.Dice,
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
					};
					return data;
				})
				.AsReadOnlyList();

			return (randomSource, rows);
		}

		private static (RandomSourceData RandomSource, IReadOnlyList<RowData> Rows) CreateWeightedRowDatas(IReadOnlyList<(int? Number1, int? Number2, string Output)> rowInfos)
		{
			var totalWeight = rowInfos.Sum(x => x.Number1!.Value);

			var dice = GetBestDice(totalWeight);
			if (dice.Count != 1)
				throw new NotImplementedException("Multiple dice not supported yet.");

			var increment = dice.Count == 1 && dice[0] % rowInfos.Count == 0 ? dice[0] / totalWeight : 1;
			var randomSource = new RandomSourceData
			{
				Kind = RandomSourceKind.Dice,
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
