using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GoldenAnvil.Utility;
using Oraculum.Data;

namespace Oraculum.Engine
{
	public sealed class RowManager
  {
    public RowManager(string? tableTitle, IReadOnlyList<RowData> rows)
    {
      m_tableTitle = tableTitle;
      m_outputLookup = rows
      .SelectMany(x => Enumerable.Range(x.Min, x.Max - x.Min + 1).Select(y => (Key: y, Value: x.Output)))
      .ToDictionary(x => (object) x.Key, x => x.Value)
      .AsReadOnly();
    }

		public RollResult GetOutput(object? key)
		{
			var value = key is null ? null : m_outputLookup.GetValueOrDefault(key);
      return new RollResult
      {
        TableTitle = m_tableTitle,
        Message = value,
      };
		}

		readonly string m_tableTitle;
    readonly ReadOnlyDictionary<object, string> m_outputLookup;
  }
  public sealed class RollResult
  {
    public string? TableTitle { get; init; }
    public string? Message { get; init; }
  }
}
