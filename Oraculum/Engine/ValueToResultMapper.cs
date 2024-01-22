using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Oraculum.Data;

namespace Oraculum.Engine;

public sealed class ValueToResultMapper
{
	public ValueToResultMapper(TableReference tableReference, RandomSourceBase randomSource, IReadOnlyList<RowDataDto> rows)
	{
		m_tableReference = tableReference ?? throw new ArgumentNullException(nameof(tableReference));
		m_valueToOutput = rows
			.SelectMany(row => randomSource.EnumerateValues(row.Min, row.Max).Select(value => (Value: value, Row: row)))
			.ToDictionary(x => x.Value, x => x.Row.Output)
			.AsReadOnly();
	}

	public RollResult GetResult(RandomValueBase value)
	{
		if (!m_valueToOutput.TryGetValue(value, out var output))
			throw new InvalidOperationException($"No row found for value {value}.");

		return new RollResult(m_tableReference, value.DisplayText, output);
	}

	readonly TableReference m_tableReference;
	readonly ReadOnlyDictionary<RandomValueBase, string> m_valueToOutput;
}
