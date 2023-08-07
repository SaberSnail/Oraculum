using System;
using System.Collections.Generic;
using System.Linq;
using static GoldenAnvil.Utility.Windows.TreeViewSelectionBehavior;

namespace Oraculum.ViewModels
{
	public static class TreeNodeUtility
	{
		public static IsDescendantDelegate IsDescendant => IsDescendantImpl;

		private static bool IsDescendantImpl(object item, object descendentCandidate)
		{
			if (item is TreeNodeBase rootNode)
			{
				foreach (var node in EnumerateNodes(rootNode, TreeNodeTraversalOrder.BreadthFirst, false, null))
				{
					if (node == descendentCandidate)
						return true;
				}
			}
			return false;
		}

		public static Stack<TreeNodeBase> FindAncestry(TreeNodeBase root, Func<TreeNodeBase, bool> predicate, bool withFilter)
		{
			var ancestry = new Stack<TreeNodeBase>();
			FindNode(root, predicate, withFilter, ancestry);
			return ancestry;
		}

		public static IEnumerable<TreeNodeBase> EnumerateNodes(TreeNodeBase root, TreeNodeTraversalOrder order, bool onlyVisible, Func<TreeNodeBase, bool>? filterPredicate)
		{
			object nextNodes = order is TreeNodeTraversalOrder.BreadthFirst ? new Queue<TreeNodeBase>() :
				order is TreeNodeTraversalOrder.DepthFirst ? new Stack<TreeNodeBase>() :
				throw new NotImplementedException($"Unhandled traversal order \"{order}\".");
			AddNodeToScan(nextNodes, root);

			while (HasNodeToScan(nextNodes))
			{
				var node = GetNextNodeToScan(nextNodes);
				if (filterPredicate?.Invoke(node) ?? true)
				{
					yield return node;
					if (node is TreeBranch branch)
					{
						var children = onlyVisible ? branch.Children.OfType<TreeNodeBase>() : branch.GetUnfilteredChildren();
						foreach (var child in children)
							AddNodeToScan(nextNodes, child);
					}
				}
			}
		}

		private static bool FindNode(TreeNodeBase root, Func<TreeNodeBase, bool> predicate, bool withFilter, Stack<TreeNodeBase> ancestry)
		{
			ancestry.Push(root);
			if (predicate.Invoke(root))
				return true;

			var foundNode = false;
			if (root is TreeBranch branch)
			{
				var children = withFilter ? branch.Children.OfType<TreeNodeBase>() : branch.GetUnfilteredChildren();
				foreach (var child in children)
				{
					foundNode = FindNode(child, predicate, withFilter, ancestry);
					if (foundNode)
						break;
				}
			}

			if (!foundNode)
				ancestry.Pop();

			return foundNode;
		}

		private static void AddNodeToScan<T>(T nodesToScan, TreeNodeBase node)
			where T : notnull
		{
			if (nodesToScan is Queue<TreeNodeBase> queue)
				queue.Enqueue(node);
			else if (nodesToScan is Stack<TreeNodeBase> stack)
				stack.Push(node);
			else
				throw new ArgumentException($"Unhandled list type \"{nodesToScan.GetType().FullName}\".");
		}

		private static TreeNodeBase GetNextNodeToScan<T>(T nodesToScan)
			where T : notnull
		{
			if (nodesToScan is Queue<TreeNodeBase> queue)
				return queue.Dequeue();
			else if (nodesToScan is Stack<TreeNodeBase> stack)
				return stack.Pop();
			else
				throw new ArgumentException($"Unhandled list type \"{nodesToScan.GetType().FullName}\".");
		}

		private static bool HasNodeToScan<T>(T nodesToScan)
			where T : notnull
		{
			if (nodesToScan is Queue<TreeNodeBase> queue)
				return queue.Count != 0;
			else if (nodesToScan is Stack<TreeNodeBase> stack)
				return stack.Count != 0;
			else
				throw new ArgumentException($"Unhandled list type \"{nodesToScan.GetType().FullName}\".");
		}
	}
}
