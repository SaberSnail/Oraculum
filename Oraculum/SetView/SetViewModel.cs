using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GoldenAnvil.Utility.Logging;
using Microsoft.VisualStudio.Threading;
using Oraculum.Data;
using Oraculum.TableEditView;

namespace Oraculum.SetView
{
	public sealed class SetViewModel : ViewModelBase
	{
		public SetViewModel(SetMetadata metadata)
		{
			m_id = metadata.Id;
			m_author = metadata.Author ?? "";
			m_version = metadata.Version;
			m_created = metadata.Created;
			m_modified = metadata.Modified;
			m_groups = metadata.Groups ?? Array.Empty<string>();
			m_title = metadata.Title ?? "";
			m_syncContextScheduler = TaskScheduler.FromCurrentSynchronizationContext();

			m_tables = new ObservableCollection<TableViewModel>();
			Tables = new ReadOnlyObservableCollection<TableViewModel>(m_tables);
		}

		public Guid Id
		{
			get => VerifyAccess(m_id);
			set => SetPropertyField(value, ref m_id);
		}

		public string Author
		{
			get => VerifyAccess(m_author);
			set => SetPropertyField(value, ref m_author);
		}

		public int Version
		{
			get => VerifyAccess(m_version);
			set => SetPropertyField(value, ref m_version);
		}

		public DateTime Created
		{
			get => VerifyAccess(m_created);
			set => SetPropertyField(value, ref m_created);
		}

		public DateTime Modified
		{
			get => VerifyAccess(m_modified);
			set => SetPropertyField(value, ref m_modified);
		}

		public IReadOnlyList<string> Groups
		{
			get => VerifyAccess(m_groups);
			set => SetPropertyField(value, ref m_groups);
		}

		public string Title
		{
			get => VerifyAccess(m_title);
			set => SetPropertyField(value, ref m_title);
		}

		public bool IsWorking
		{
			get => VerifyAccess(m_isWorking);
			set => SetPropertyField(value, ref m_isWorking);
		}

		public ReadOnlyObservableCollection<TableViewModel> Tables { get; }

		public async void LoadTablesIfNeeded()
		{
			try
			{
				await LoadTablesIfNeededAsync();
			}
			catch (OperationCanceledException)
			{
			}
		}

		private async Task LoadTablesIfNeededAsync()
		{
			VerifyAccess();
			if (m_isLoaded)
				return;

			using var _ = Log.TimedInfo($"Loading set: {Title}");

			await m_syncContextScheduler;
			IsWorking = true;
			var tableId = Id;

			try
			{
				await TaskScheduler.Default;

				var tables = await AppModel.Instance.Data.GetTablesInSet(tableId);
				await Task.Delay(TimeSpan.FromSeconds(4));

				await m_syncContextScheduler;

				m_tables.Clear();
				foreach (var table in tables)
					m_tables.Add(new TableViewModel(table));
			}
			finally
			{
				await m_syncContextScheduler;
				IsWorking = false;
				m_isLoaded = true;
			}
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(SetViewModel));

		private Guid m_id;
		private string m_author;
		private int m_version;
		private DateTime m_created;
		private DateTime m_modified;
		private IReadOnlyList<string> m_groups;
		private string m_title;
		private ObservableCollection<TableViewModel> m_tables;
		private bool m_isWorking;
		private bool m_isLoaded;
		private TaskScheduler m_syncContextScheduler;
	}
}
