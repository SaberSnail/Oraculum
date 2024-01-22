using Oraculum.Data;

namespace Oraculum.Engine
{
	public sealed class RollResult
  {
    public RollResult(TableReference table, string value, string output)
    {
			Table = table;
			Key = value;
			Output = output;
		}

    public TableReference Table { get; init; }
    public string Key { get; init; }
    public string Output { get; init; }
  }
}
