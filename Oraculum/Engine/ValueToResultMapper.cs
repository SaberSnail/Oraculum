using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;
using Oraculum.Data;
using Oraculum.UI;

namespace Oraculum.Engine;

public sealed class ValueToResultMapper
{
	public ValueToResultMapper()
	{
		m_tables = [];
	}

	public void AddTable(TableReference tableReference, RandomSourceBase randomSource, IReadOnlyList<RowDataDto> rows)
	{
		var valueToOutput = rows
			.SelectMany(row => randomSource.EnumerateValues(row.Min, row.Max).Select(value => (Value: value, Row: row)))
			.ToDictionary(x => x.Value, x => x.Row.Output)
			.AsReadOnly();
		m_tables.Add(tableReference, valueToOutput);
	}

	public RollResult GetResult(TableReference table, IReadOnlyList<RandomValueBase> allValues)
	{
		var values = allValues.GetEnumerator();
		values.MoveNext();
		var outputs = m_tables[table];
		var output = outputs[values.Current];

		output = TokenStringUtility.ReplaceTableReferences(output, t => GetOutput(t, values));
		var displayText = allValues.Where(x => x is not FixedValue).Select(x => x.DisplayText).Join("; ");

		return new RollResult(table, displayText, output ?? "");
	}

	private string? GetOutput(TableReference table, IEnumerator<RandomValueBase> values)
	{
		if (!values.MoveNext())
			return null;
		var outputs = m_tables[table];
		return outputs[values.Current];
	}

	readonly Dictionary<TableReference, IReadOnlyDictionary<RandomValueBase, string>> m_tables;
}
