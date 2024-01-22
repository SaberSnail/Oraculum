using System;
using System.Collections.Generic;

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
}
