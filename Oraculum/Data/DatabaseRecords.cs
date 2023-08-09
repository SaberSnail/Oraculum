using System;
using System.Collections.Generic;
using Oraculum.Engine;

namespace Oraculum.Data
{
	public static class StaticData
	{
		public static readonly Guid AllSetId = new Guid("53E1D905-7A43-4E8D-9595-5C61464DEC19");

		public static readonly SetMetadata AllSet = new SetMetadata
		{
			Id = AllSetId,
			Author = "",
			Version = 1,
			Created = DateTime.Now,
			Modified = DateTime.Now,
			Groups = Array.Empty<string>(),
			Title = "All",
		};
	}

	public readonly record struct SetMetadata(Guid Id, string Author, int Version, DateTime Created, DateTime Modified, IReadOnlyList<string> Groups, string Title);

	public readonly record struct TableMetadata(Guid Id, string Author, int Version, DateTime Created, DateTime Modified, IReadOnlyList<string> Groups, string Title, RandomSourceData RandomSource);

	public readonly record struct RandomSourceData(RandomSourceKind Kind, IReadOnlyList<int> Dice);

	public readonly record struct RowData(int Min, int Max, string Output, Guid? Next);
}
