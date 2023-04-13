using System;
using System.Collections.Generic;
using System.Linq;
using Oraculum.Data;

namespace Oraculum.Engine
{
	public abstract class RandomSourceBase
  {
    public static RandomSourceBase Create(RandomSourceData data)
    {
      return data.Kind switch
      {
        RandomSourceKind.Dice => new DiceSource(data.Dice.ToArray()),
        _ => throw new NotImplementedException($"Create not handled for kind {data.Kind}."),
      };
    }

    public abstract RandomSourceKind Kind { get; }

		public abstract (object Key, IReadOnlyList<object> Values) GenerateResult();
  }
}
