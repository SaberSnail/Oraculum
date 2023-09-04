using System;
using System.Collections.Generic;
using System.Linq;
using GoldenAnvil.Utility;
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
			Title = "All",
		};
	}

	public readonly record struct SetMetadata
	{
		public SetMetadata(Guid Id, string Author, int Version, DateTime Created, DateTime Modified, string Title, IReadOnlyList<string>? Groups = null)
		{
			this.Id = Id;
			this.Author = Author;
			this.Version = Version;
			this.Created = Created;
			this.Modified = Modified;
			this.Title = Title;
			this.Groups = Groups ?? Array.Empty<string>();
		}

		public Guid Id { get; init; }
		public string Author { get; init; }
		public int Version { get; init; }
		public DateTime Created { get; init; }
		public DateTime Modified { get; init; }
		public string Title { get; init; }
		public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
	}

	public readonly record struct TableMetadata
	{
		public TableMetadata(Guid Id, string? Source, string? Author, int Version, DateTime Created, DateTime Modified, string Title, IReadOnlyList<RandomSourceData> RandomPlan, IReadOnlyList<string>? Groups = null)
		{
			this.Id = Id;
			this.Source = Source;
			this.Author = Author;
			this.Version = Version;
			this.Created = Created;
			this.Modified = Modified;
			this.Title = Title;
			this.RandomPlan = RandomPlan;
			this.Groups = Groups ?? Array.Empty<string>();
		}

		public Guid Id { get; init; }
		public string? Source { get; init; }
		public string? Author { get; init; }
		public int Version { get; init; }
		public DateTime Created { get; init; }
		public DateTime Modified { get; init; }
		public string Title { get; init; }
		public IReadOnlyList<RandomSourceData> RandomPlan { get; init; } = Array.Empty<RandomSourceData>();
		public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
	}

	public struct RandomSourceData : IEquatable<RandomSourceData>
	{
		public RandomSourceData(RandomSourceKind Kind, IReadOnlyList<int>? Dice = null)
		{
			this.Kind = Kind;
			this.Dice = Dice ?? Array.Empty<int>();
		}

		public RandomSourceKind Kind { get; init; }
		public IReadOnlyList<int> Dice { get; init; } = Array.Empty<int>();

		public override bool Equals(object? that) =>
			that is RandomSourceData data && Equals(data);

		public bool Equals(RandomSourceData that) =>
			(Kind == that.Kind) && Dice.SequenceEqual(that.Dice ?? Array.Empty<int>());

		public override int GetHashCode() =>
			HashCodeUtility.CombineHashCodes(Kind.GetHashCode(), Dice.GetHashCode());

		public static bool operator ==(RandomSourceData left, RandomSourceData right) =>
			left.Equals(right);

		public static bool operator !=(RandomSourceData left, RandomSourceData right) =>
			!left.Equals(right);
	}

	public readonly record struct RowData(int Min, int Max, string Output, Guid? Next);
}
