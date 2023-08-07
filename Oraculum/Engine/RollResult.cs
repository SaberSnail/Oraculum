using System;

namespace Oraculum.Engine
{
	public sealed class RollResult
  {
    public Guid TableId { get; init; }
    public string? TableTitle { get; init; }
    public string? Key { get; init; }
    public string? Output { get; init; }
  }
}
