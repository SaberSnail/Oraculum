using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GoldenAnvil.Utility;
using GoldenAnvil.Utility.Logging;
using Oraculum.Data;

namespace Oraculum.ViewModels
{
	public sealed class TableViewModel : ViewModelBase
	{
		public TableViewModel(TableMetadata metadata)
		{
			m_id = metadata.Id;
			m_author = metadata.Author ?? "";
			m_version = metadata.Version;
			m_created = metadata.Created;
			m_modified = metadata.Modified;
			m_groups = metadata.Groups ?? Array.Empty<string>();
			m_title = metadata.Title ?? "";

			RandomValue = 100;
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

		public bool ShouldAnimateRandomValue
		{
			get => VerifyAccess(m_shouldAnimateRandomValue);
			private set => SetPropertyField(value, ref m_shouldAnimateRandomValue);
		}

		public int RandomValue
		{
			get => VerifyAccess(m_randomValue);
			private set => SetPropertyField(value, ref m_randomValue);
		}

		public void SetRandomValue()
		{
			ShouldAnimateRandomValue = true;
			RandomValue = AppModel.Instance.Random.NextRoll(1, 100);
			ShouldAnimateRandomValue = false;
		}

		public void OnRandomValueDisplayed()
		{
			Log.Info($"Finished rolling, got a {RandomValue}");
		}

		public async Task LoadRowsIfNeededAsync()
		{
			if (m_isLoaded)
				return;

			Log.Info($"Loading table: {Title}");
			m_isLoaded = true;
		}

		private static ILogSource Log { get; } = LogManager.CreateLogSource(nameof(TableViewModel));

		private Guid m_id;
		private string m_author;
		private int m_version;
		private DateTime m_created;
		private DateTime m_modified;
		private IReadOnlyList<string> m_groups;
		private string m_title;
		private bool m_isLoaded;
		private int m_randomValue;
		private bool m_shouldAnimateRandomValue;
	}
}
