using FluentAssertions;
using Oraculum.Data;
using Oraculum.Engine;
using ProtoBuf;

namespace Oraculum.Tests
{
	public class BinarySerializationTests
	{
		[Test]
		public void SetMetadataRoundTrip()
		{
			var originalData = CreateSet();

			using var stream = new MemoryStream();

			Serializer.Serialize(stream, originalData);
			stream.Seek(0, SeekOrigin.Begin);

			var newData = Serializer.Deserialize<SetMetadata>(stream);
			
			newData.Should().BeEquivalentTo(originalData);
		}

		[Test]
		public void RowDataRoundTrip()
		{
			var originalData = CreateRow();

			using var stream = new MemoryStream();

			Serializer.Serialize(stream, originalData);
			stream.Seek(0, SeekOrigin.Begin);

			var newData = Serializer.Deserialize<RowDataDto>(stream);

			newData.Should().BeEquivalentTo(originalData);
		}

		private static SetMetadata CreateSet()
		{
			var setId = Guid.NewGuid();
			var setDateTime = DateTime.Now;

			return new SetMetadata
			{
				Id = setId,
				Author = "SaberSnail",
				Version = 1,
				Created = setDateTime,
				Modified = setDateTime,
				Groups = new List<string> { "RPG", "Ironsworn" },
				Title = "Ironsworn",
			};
		}

		private static TableMetadataDto CreateTable()
		{
			var tableId = Guid.NewGuid();
			var tableDateTime = DateOnly.FromDateTime(DateTime.Now);

			return new TableMetadataDto(
				tableId: tableId,
				title: "50/50",
				source: "Ironsworn",
				author: "SaberSnail",
				version: 1,
				created: tableDateTime,
				modified: tableDateTime,
				description: null,
				randomPlan: new RandomPlan(RandomSourceKind.DiceSequence, 100),
				groups: new[] { "Ironsworn", "Oracle" });
		}

		private static RowDataDto CreateRow()
		{
			return new RowDataDto
			{
				Min = [1],
				Max = [50],
				Output = "no"
			};
		}
	}
}