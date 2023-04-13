using System;

namespace Oraculum.ViewModels
{
	public abstract class TreeNodeBase : ViewModelBase
	{
		public string? Title
		{
			get => VerifyAccess(m_title);
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

		private string? m_title;
		private string? m_currentFilter;
		private bool? m_lastMatchesFilter;
	}
}
