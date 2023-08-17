using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using GoldenAnvil.Utility;

namespace Oraculum.ViewModels
{
	public sealed class TreeBranch : TreeNodeBase
	{
		public TreeBranch()
		{
			m_children = new List<TreeNodeBase>();
			Children = CollectionViewSource.GetDefaultView(m_children);
			Children.Filter = x => (x as TreeNodeBase)?.MatchesCurrentFilter() ?? false;
			Children.SortDescriptions.Add(new SortDescription(nameof(IsBranch), ListSortDirection.Descending));
			Children.SortDescriptions.Add(new SortDescription(nameof(Title), ListSortDirection.Ascending));
		}

		public override bool IsBranch => true;

		public ICollectionView Children { get; }

		public bool IsExpanded
		{
			get => VerifyAccess(m_isExpanded);
			set
			{
				if (SetPropertyField(value, ref m_isExpanded) && value)
				{
					if (m_children.Count == 1 && m_children[0] is TreeBranch childBranch)
						childBranch.IsExpanded = true;
				}
			}
		}

		public IEnumerable<TreeNodeBase> GetUnfilteredChildren() => m_children;

		public IEnumerable<TreeBranch> GetChildBranches() => m_children.OfType<TreeBranch>();

		public void AddChild(TreeNodeBase child)
		{
			VerifyAccess();

			if (child.Parent is not null)
				child.Parent.RemoveChild(child);

			child.Parent = this;
			m_children.Add(child);
			child.PropertyChanged += OnChildPropertyChanged;
		}

		public void RemoveChild(TreeNodeBase child)
		{
			VerifyAccess();
			if (m_children.Remove(child))
			{
				child.Parent = null;
				child.PropertyChanged -= OnChildPropertyChanged;
			}
		}

		protected override bool MatchesCurrentFilterCore() =>
			base.MatchesCurrentFilterCore() || m_children.Any(x => x.MatchesCurrentFilter());

		protected override void SetCurrentFilterCore(string? filterText, bool force)
		{
			var adjustedFilterText = filterText;
			if (base.MatchesCurrentFilterCore())
				adjustedFilterText = null;

			foreach (var child in m_children)
				child.SetCurrentFilter(adjustedFilterText, force);
			Children.Refresh();
		}

		private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			var child = (TreeNodeBase) sender!;
			if (e.HasChanged(nameof(IsSelected)) && child.IsSelected)
				IsExpanded = true;
			else if (e.HasChanged(nameof(IsExpanded)) && ((TreeBranch) child).IsExpanded)
				IsExpanded = true;
		}

		private readonly List<TreeNodeBase> m_children;

		private bool m_isExpanded;
	}
}
