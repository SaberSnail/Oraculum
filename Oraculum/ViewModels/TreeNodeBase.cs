using System;

namespace Oraculum.ViewModels
{
	public abstract class TreeNodeBase : ViewModelBase
	{
		public TreeBranch? Parent
		{
			get => VerifyAccess(m_parent);
			set => SetPropertyField(value, ref m_parent);
		}

		public bool IsSelected
		{
			get => VerifyAccess(m_isSelected);
			set => SetPropertyField(value, ref m_isSelected);
		}

		public string Title
		{
			get => VerifyAccess(m_title ?? "");
			set => SetPropertyField(value, ref m_title);
		}

		public void SetCurrentFilter(string? filterText, bool force)
		{
			VerifyAccess();
			if (!force && m_currentFilter == filterText)
				return;

			m_currentFilter = filterText;
			m_lastMatchesFilter = null;

			SetCurrentFilterCore(filterText, force);
		}

		public bool MatchesCurrentFilter()
		{
			if (m_lastMatchesFilter.HasValue)
				return m_lastMatchesFilter.Value;

			m_lastMatchesFilter = MatchesCurrentFilterCore();
			return m_lastMatchesFilter.Value;
		}

		protected string? GetCurrentFilter() => m_currentFilter;

		protected virtual bool MatchesCurrentFilterCore()
		{
			var filterText = GetCurrentFilter();

			if (string.IsNullOrEmpty(filterText))
				return true;

			if (string.IsNullOrEmpty(Title))
				return false;

			return Title.Contains(filterText, StringComparison.CurrentCultureIgnoreCase);
		}

		protected virtual void SetCurrentFilterCore(string? filterText, bool force)
		{
		}

		private TreeBranch? m_parent;
		private bool m_isSelected;
		private string? m_title;
		private string? m_currentFilter;
		private bool? m_lastMatchesFilter;
	}
}
