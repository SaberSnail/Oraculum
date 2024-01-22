using System;

namespace Oraculum.Data
{
	public sealed partial class DataManager
	{
		private sealed class TableReferenceImpl : TableReference
		{
			public TableReferenceImpl(Guid id, string title, DataManager manager)
				: base(id, title, manager.m_dispatcher)
			{
				m_manager = manager;
			}

			protected override void OnTitleChanged() => m_manager.UpdateTableTitle(Id, Title);

			private readonly DataManager m_manager;
		}
	}
}
