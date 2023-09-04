using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GoldenAnvil.Utility;
using Oraculum.Data;

namespace Oraculum.Engine;

public sealed class ValueToResultMapper
{
	public ValueToResultMapper(Guid tableId, string? tableTitle, IReadOnlyList<RowData> rows)
	{
		m_tableId = tableId;
		m_tableTitle = tableTitle;
		m_valueToRow = rows
			.SelectMany(x => Enumerable.Range(x.Min, x.Max - x.Min + 1).Select(y => KeyValuePair.Create(y, x)))
			.ToDictionary(x => x.Key, x => (RowData?) x.Value)
			.AsReadOnly();
	}

	public RollResult GetResult(RandomValueBase value)
	{
		var row = m_valueToRow.GetValueOrDefault(value.Value);
		return new RollResult
		{
			TableId = m_tableId,
			TableTitle = m_tableTitle,
			Key = value.DisplayText,
			Output = row?.Output,
			Next = row?.Next,
		};
	}

	readonly Guid m_tableId;
	readonly string? m_tableTitle;
	readonly ReadOnlyDictionary<int, RowData?> m_valueToRow;
}
