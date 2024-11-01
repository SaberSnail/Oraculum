using System;
using System.Collections.Generic;
using System.Diagnostics;
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
			Title = OurResources.AllTablesSetTitle,
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

	public class TableMetadataDto
	{
		public TableMetadataDto(Guid tableId, string title, string? source, string? author, int version, DateOnly created, DateOnly? modified, string? description, RandomPlan randomPlan, IReadOnlyList<string>? groups)
		{
			TableId = tableId;
			Title = title;
			Source = source;
			Author = author;
			Version = version;
			Created = created;
			Modified = modified ?? created;
			Description = description;
			RandomPlan = randomPlan;
			Groups = groups ?? Array.Empty<string>();
		}

		public Guid TableId { get; init; }
		public string Title { get; init; }
		public string? Source { get; init; }
		public string? Author { get; init; }
		public int Version { get; init; }
		public DateOnly Created { get; init; }
		public DateOnly Modified { get; init; }
		public string? Description { get; init; }
		public RandomPlan RandomPlan { get; init; }
		public IReadOnlyList<string> Groups { get; init; }
	}

	[DebuggerDisplay("{GetDebugDisplayString()}")]
	public class RandomPlan : IEquatable<RandomPlan>
	{
		public RandomPlan(RandomSourceKind kind, params int[] configurations)
			: this(kind, (IReadOnlyList<int>) configurations)
		{
		}

		public RandomPlan(RandomSourceKind kind, IReadOnlyList<int> configurations)
		{
			Kind = kind;
			Configurations = configurations;
		}

		public RandomSourceKind Kind { get; init; }
		public IReadOnlyList<int> Configurations { get; init; }

		public override bool Equals(object? that) => Equals(that as RandomPlan);

		public bool Equals(RandomPlan? that) =>
			that is { } && Kind == that.Kind && Configurations.SequenceEqual(that.Configurations);

		public override int GetHashCode() =>
			HashCodeUtility.CombineHashCodes(Kind.GetHashCode(), Configurations.GetHashCode());

		public static bool operator ==(RandomPlan left, RandomPlan right) =>
			left.Equals(right);

		public static bool operator !=(RandomPlan left, RandomPlan right) =>
			!left.Equals(right);

		private string GetDebugDisplayString() => $"{Kind}-{Configurations.Select(x => x.ToString()).Join(",")}";
	}

	public readonly record struct RowDataDto(IReadOnlyList<int> Min, IReadOnlyList<int> Max, string Output);
}
