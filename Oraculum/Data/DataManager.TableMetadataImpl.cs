using System;
using System.ComponentModel;

namespace Oraculum.Data
{
	public sealed partial class DataManager
	{
		private sealed class TableMetadataImpl : TableMetadata, IDisposable
		{
			public TableMetadataImpl(TableReferenceImpl tableReference, TableMetadataDto dto, DataManager manager)
				: base(dto.Version, dto.Created, dto.Modified, dto.Source, dto.Author, dto.RandomPlan, dto.Groups, manager.m_dispatcher)
			{
				m_manager = manager;
				TableReference = tableReference;

				TableReference.PropertyChanged += TableReference_PropertyChanged;
			}

			public override TableReferenceImpl TableReference { get; }

			public void Dispose() =>
				TableReference.PropertyChanged -= TableReference_PropertyChanged;

			protected override void OnSourceChanged() =>
				m_manager.UpdateTableSource(TableReference.Id, Source);

			protected override void OnAuthorChanged() =>
				m_manager.UpdateTableAuthor(TableReference.Id, Author);

			protected override void OnRandomPlanChanged() =>
				m_manager.UpdateTableRandomPlan(TableReference.Id, RandomPlan);

			protected override void OnGroupsChanged() =>
				m_manager.UpdateTableGroups(TableReference.Id, Groups);

			private void TableReference_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
				RaisePropertyChanged(nameof(Title));

			private readonly DataManager m_manager;
		}
	}
}
