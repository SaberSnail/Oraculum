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
		public void TableMetadataRoundTrip()
		{
			var originalData = CreateTable();

			using var stream = new MemoryStream();

			Serializer.Serialize(stream, originalData);
			stream.Seek(0, SeekOrigin.Begin);

			var newData = Serializer.Deserialize<TableMetadata>(stream);

			newData.Should().BeEquivalentTo(originalData);
		}

		[Test]
		public void RowDataRoundTrip()
		{
			var originalData = CreateRow();

			using var stream = new MemoryStream();

			Serializer.Serialize(stream, originalData);
			stream.Seek(0, SeekOrigin.Begin);

			var newData = Serializer.Deserialize<RowData>(stream);

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

		private static TableMetadata CreateTable()
		{
			var tableId = Guid.NewGuid();
			var tableDateTime = DateTime.Now;

			return new TableMetadata
			{
				Id = tableId,
				Author = "SaberSnail",
				Version = 1,
				Created = tableDateTime,
				Modified = tableDateTime,
				Groups = new[] { "Ironsworn", "Oracle" },
				Title = "50/50",
				RandomPlan = new[] { new RandomSourceData { Dice = new[] { 100 } } },
			};
		}

		private static RowData CreateRow()
		{
			return new RowData
			{
				Min = 1,
				Max = 50,
				Output = "no"
			};
		}
	}
}