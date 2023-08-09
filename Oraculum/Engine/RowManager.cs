using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GoldenAnvil.Utility;
using Oraculum.Data;

namespace Oraculum.Engine
{
	public sealed class RowManager
  {
    public RowManager(Guid tableId, string? tableTitle, IReadOnlyList<RowData> rows)
    {
      m_tableId = tableId;
      m_tableTitle = tableTitle;
      m_outputLookup = rows
      .SelectMany(x => Enumerable.Range(x.Min, x.Max - x.Min + 1).Select(y => KeyValuePair.Create(y, x)))
      .ToDictionary(x => (object) x.Key, x => (RowData?) x.Value)
      .AsReadOnly();
    }

		public RollResult GetOutput(object? key)
		{
			var value = key is null ? null : m_outputLookup.GetValueOrDefault(key);
      return new RollResult
      {
        TableId = m_tableId,
        TableTitle = m_tableTitle,
        Key = key?.ToString() ?? "",
        Output = value?.Output,
        Next = value?.Next,
      };
		}

    readonly Guid m_tableId;
		readonly string? m_tableTitle;
    readonly ReadOnlyDictionary<object, RowData?> m_outputLookup;
  }
}
