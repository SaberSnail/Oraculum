using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Oraculum.Data
{
	public abstract class TableMetadata : ViewModelBase
	{
		public abstract TableReference TableReference { get; }

		public Guid Id => TableReference.Id;

		public DateOnly Created { get; init; }

		public DateOnly Modified
		{
			get => VerifyAccess(m_modified);
			protected set => SetPropertyField(value, ref m_modified);
		}

		public int Version
		{
			get => VerifyAccess(m_version);
			protected set => SetPropertyField(value, ref m_version);
		}

		public string Title
		{
			get => TableReference.Title;
			set => TableReference.Title = value;
		}

		public string? Source
		{
			get => VerifyAccess(m_source);
			set
			{
				if (SetPropertyField(value, ref m_source))
					OnSourceChanged();
			}
		}

		public string? Author
		{
			get => VerifyAccess(m_author);
			set
			{
				if (SetPropertyField(value, ref m_author))
					OnAuthorChanged();
			}
		}

		public RandomPlan RandomPlan
		{
			get => VerifyAccess(m_randomPlan);
			set
			{
				if (SetPropertyField(value, ref m_randomPlan))
					OnRandomPlanChanged();
			}
		}

		public IReadOnlyList<string> Groups
		{
			get => VerifyAccess(m_groups);
			set
			{
				if (SetPropertyField(value, ref m_groups))
					OnGroupsChanged();
			}
		}

		protected abstract void OnSourceChanged();

		protected abstract void OnAuthorChanged();

		protected abstract void OnRandomPlanChanged();

		protected abstract void OnGroupsChanged();

		protected TableMetadata(int version, DateOnly created, DateOnly modified, string? source, string? author, RandomPlan randomPlan, IReadOnlyList<string> groups, Dispatcher dispatcher)
			: base(dispatcher)
		{
			m_version = version;
			Created = created;
			m_modified = modified;
			m_source = source;
			m_author = author;
			m_randomPlan = randomPlan;
			m_groups = groups;
		}

		private DateOnly m_modified;
		private int m_version;
		private string? m_source;
		private string? m_author;
		private RandomPlan m_randomPlan;
		private IReadOnlyList<string> m_groups;
	}
}
