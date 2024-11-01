using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoldenAnvil.Utility.Windows.Async;
using GoldenAnvil.Utility;
using Microsoft.VisualStudio.Threading;
using Oraculum.Data;
using Oraculum.UI;

namespace Oraculum.Engine;

public static class RandomPlanUtility
{
	public static IReadOnlyList<int>? MergeConfigurations(IEnumerable<IReadOnlyList<int>> configs, Func<int, int, int?> mergeFunc)
	{
		var mergedConfig = new List<int>();
		int? configSize = null;
		foreach (var config in configs)
		{
			if (configSize is null)
			{
				configSize = config.Count;
				mergedConfig.AddRange(config);
			}
			else
			{
				for (int index = 0; index < configSize; index++)
				{
					var merged = mergeFunc(mergedConfig[index], config[index]);
					if (merged is null)
						return null;
					mergedConfig[index] = (int) merged.Value;
				}
			}
		}

		return mergedConfig;
	}

	public static async Task<(IReadOnlyList<RandomPlan>? RandomPlans, IReadOnlyDictionary<TableReference, (RandomSourceBase RandomSource, IReadOnlyList<RowDataDto> Rows)> Tables)> GetTableRowsAndExtrasAsync(DataManager data, TableReference rootTable, RandomPlan rootRandomPlan, TaskStateController state)
	{
		await state.ToThreadPool();
		var rows = await data.GetRowsAsync(rootTable.Id, state.CancellationToken).ConfigureAwait(false);
		await state.ToSyncContext();

		var tableNodes = new Dictionary<TableReference, TableNode>();
		var rootNode = new TableNode { Table = rootTable, TableRandomPlan = rootRandomPlan, Rows = rows };
		tableNodes[rootTable] = rootNode;
		await LoadChildrenAsync(rootNode, tableNodes, data, state).ConfigureAwait(false);

		var randomPlans = rootNode.AllRandomPlans.AsReadOnlyList();
		var tables = tableNodes.ToDictionary(x => x.Key, x => (RandomSourceBase.Create(x.Value.TableRandomPlan!), x.Value.Rows));
		return (randomPlans, tables);
	}

	private static async Task LoadChildrenAsync(TableNode node, Dictionary<TableReference, TableNode> loadedNodes, DataManager data, TaskStateController state)
	{
		if (!node.IsLoadingChildren)
			return;

		var groups = new List<IReadOnlyList<TableNode>>();
		foreach (var row in node.Rows)
		{
			var tableRefs = TokenStringUtility.GetTableReferences(row.Output);
			await LoadUnloadedTablesAsync(tableRefs, loadedNodes, data, state).ConfigureAwait(false);

			var group = tableRefs.Count == 0 ? [TableNode.Null] : tableRefs.Select(x => loadedNodes[x]).AsReadOnlyList();
			if (!groups.Any(x => x.SequenceEqual(group)))
				groups.Add(group);
		}

		if (groups.Any(x => x.Any(y => y == TableNode.Null)))
		{
			// rows without references can't be used
		}
		else if (groups[0].Count == 0)
		{
			// there are no children
		}
		else if (groups.Any(x => x.Any(y => y.IsLoadingChildren)))
		{
			// the row is recursively referencing a table, these children can't be handled
		}
		else
		{
			// make sure each group has the same random plans
			var masterPlans = groups
				.Select(x => x.SelectMany(y => y.AllRandomPlans!).AsReadOnlyList())
				.OrderBy(x => x.Count)
				.First();
			var areChildrenValid = groups.All(group =>
			{
				var nestedChildPlans = group.SelectMany(x => x.AllRandomPlans!);
				return nestedChildPlans.StartsWith(masterPlans);
			});
			if (areChildrenValid)
				node.ChildRandomPlans = masterPlans;
		}

		node.IsLoadingChildren = false;
	}

	private static async Task LoadUnloadedTablesAsync(IReadOnlyList<TableReference> tables, Dictionary<TableReference, TableNode> loadedNodes, DataManager data, TaskStateController state)
	{
		var unloadedTables = tables.Where(x => !loadedNodes.ContainsKey(x)).AsReadOnlyList();
		if (unloadedTables.Count == 0)
			return;

		await state.ToThreadPool();
		var metadatas = await data.GetTableMetadatasAsync(unloadedTables.Select(x => x.Id), state.CancellationToken).ConfigureAwait(false);
		await state.ToSyncContext();

		List<TableNode> newNodes = [];
		foreach (var table in unloadedTables)
		{
			await state.ToThreadPool();
			var newRows = await data.GetRowsAsync(table.Id, state.CancellationToken).ConfigureAwait(false);
			await state.ToSyncContext();

			var newNode = new TableNode
			{
				Table = table,
				TableRandomPlan = metadatas.First(x => x.Id == table.Id).RandomPlan,
				Rows = newRows,
			};
			loadedNodes.Add(table, newNode);
			newNodes.Add(newNode);
		}

		foreach (var node in newNodes)
			await LoadChildrenAsync(node, loadedNodes, data, state).ConfigureAwait(false);
	}

	private sealed class TableNode
	{
		public static readonly TableNode Null = new() { Rows = [], IsLoadingChildren = false };

		public TableReference? Table { get; init; }
		public RandomPlan? TableRandomPlan { get; init; }
		public required IReadOnlyList<RowDataDto> Rows { get; init; }
		public IReadOnlyList<RandomPlan> ChildRandomPlans { get; set; } = [];
		public IEnumerable<RandomPlan>? AllRandomPlans => TableRandomPlan is null ? null : ChildRandomPlans.Prepend(TableRandomPlan);
		public bool IsLoadingChildren { get; set; } = true;
	}
}
