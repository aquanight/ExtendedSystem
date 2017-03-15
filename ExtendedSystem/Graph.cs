using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExtendedSystem
{

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix")]
	public sealed class Graph<T> : ISet<T>, IEquatable<Graph<T>>
	{
		private Dictionary<T, HashSet<T>> _graph;

		/// <summary>
		/// Returns the IEqualityComparer employed in the graph, which dictates equatablity of the vertices, and hashing.
		/// </summary>
		public IEqualityComparer<T> Comparer
		{
			get
			{
				return this._graph.Comparer;
			}
		}

		/// <summary>
		/// Creates a new empty graph with the default IEqualityComparer.
		/// </summary>
		public Graph()
		{
			this._graph = new Dictionary<T, HashSet<T>>();
		}

		/// <summary>
		/// Creates a new empty graph with the specified IEqualityComparer.
		/// </summary>
		public Graph(IEqualityComparer<T> comparer)
		{
			this._graph = new Dictionary<T, HashSet<T>>(comparer);
		}

		/// <summary>
		/// Creates a new empty graph with the specified capacity and default IEqualityComparer.
		/// </summary>
		public Graph(int capacity)
		{
			this._graph = new Dictionary<T, HashSet<T>>(capacity);
		}

		/// <summary>
		/// Creates a new empty graph with the specified capacity and IEqualityComparer.
		/// </summary>
		public Graph(int capacity, IEqualityComparer<T> comparer)
		{
			this._graph = new Dictionary<T, HashSet<T>>(capacity, comparer);
		}

		/// <summary>
		/// Creates a new graph populated with the specified initial vertices and the default IEqualityComparer.
		/// </summary>
		public Graph(ISet<T> vertices)
		{
			this._graph = new Dictionary<T, HashSet<T>>(vertices.ToDictionary((v) => v, (v) => new HashSet<T>()));
		}

		/// <summary>
		/// Creates a new graph populated with the specified initial vertices and the specified IEqualityComparer.
		/// </summary>
		public Graph(ISet<T> vertices, IEqualityComparer<T> comparer)
		{
			this._graph = new Dictionary<T, HashSet<T>>(vertices.ToDictionary((v) => v, (v) => new HashSet<T>()), comparer);
		}

		/// <summary>
		/// Returns the number of vertices in the graph.
		/// </summary>
		public int Count
		{
			get
			{
				return this._graph.Count;
			}
		}

		/// <summary>
		/// Returns the number of edges in the graph.
		/// </summary>
		public int EdgeCount
		{
			get
			{
				return this._graph.Sum((kvp) => kvp.Value.Count);
			}
		}

		/// <summary>
		/// Determines if this graph is read-only. As such it always returns false.
		/// </summary>
		bool ICollection<T>.IsReadOnly
		{
			get
			{
				return false;
			}
		}

		/// <summary>
		/// Adds the specified vertex to the graph.
		/// Returns true if successful, false if not (because the vertex already exists).
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Add(T item)
		{
			if (this._graph.ContainsKey(item))
				return false;
			this._graph.Add(item, new HashSet<T>(this.Comparer));
			return true;
		}

		/// <summary>
		/// Removes all vertices and edges from the graph.
		/// </summary>
		public void Clear()
		{
			foreach (var v in this._graph)
				v.Value.Clear();
			this._graph.Clear();
		}

		/// <summary>
		/// Returns true if the specified vertex is part of the graph and false if not.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Contains(T item)
		{
			return this._graph.ContainsKey(item);
		}

		/// <summary>
		/// Returns true if there exists an edge from the vertex specified by "from" to the vertex specified by "to", false otherwise.
		/// Returns false if neither vertex is part of the graph.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <returns></returns>
		public bool IsAdjacent(T from, T to)
		{
			return this._graph.ContainsKey(from) && this._graph[from].Contains(to);
		}

		/// <summary>
		/// Adds an edge from the vertex specified by "from" to the vertex specified by "to".
		/// Both vertices, and the requested edge, shall exist in the graph after this operation completes.
		/// If either vertex is not a member of the graph, then it is added.
		/// Returns true if the edge is added and false if not (because it already existed).
		/// Edges are directed, so to make a bidirectional edge you must call AddEdge a second time with the parameters reversed.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <returns></returns>
		public bool AddEdge(T from, T to)
		{
			Add(from);
			Add(to);
			return this._graph[from].Add(to);
		}

		/// <summary>
		/// Removes the edge from the vertex specified by "from" to the vertex specified by "to".
		/// If neither vertex is a member of the graph, or the edge already does not exist, then the return value is false.
		/// Otherwise, the edge is removed and the return value is true.
		/// Note that edges are directed, and an edge from "to" to "from" is not removed. To remove that edge too, call the function again with
		/// the parameters reversed.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		/// <returns></returns>
		public bool RemoveEdge(T from, T to)
		{
			return this._graph.ContainsKey(from) && this._graph[from].Remove(to);
		}

		/// <summary>
		/// Copies the set of all vertices from the graph to the specified array.
		/// </summary>
		/// <param name="array"></param>
		/// <param name="arrayIndex"></param>
		public void CopyTo(T[] array, int arrayIndex)
		{
			this._graph.Keys.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Has the same effect as the RemoveVertices method.
		/// </summary>
		/// <param name="other"></param>
		void ISet<T>.ExceptWith(IEnumerable<T> other)
		{
			RemoveVertices(other);
		}

		public void RemoveVertices(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			foreach (var e in other)
			{
				if (this._graph.ContainsKey(e))
					this._graph[e].Clear();
				this._graph.Remove(e);
			}
			foreach (var h in this._graph.Values)
				h.ExceptWith(other);
		}

		/// <summary>
		/// Enumerates the vertices in this graph.
		/// </summary>
		/// <returns></returns>
		public IEnumerator<T> GetEnumerator()
		{
			return this._graph.Keys.GetEnumerator();
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1006:DoNotNestGenericTypesInMemberSignatures")]
		public IEnumerator<KeyValuePair<T, T>> GetEdgeEnumerator()
		{
			foreach (var kvp in this._graph)
				foreach (var v in kvp.Value)
					yield return new KeyValuePair<T, T>(kvp.Key, v);
		}

		/// <summary>
		/// Removes all vertices that are not also in the set specified by other.
		/// </summary>
		/// <param name="other"></param>
		public void IntersectWith(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			var rm = this._graph.Keys.Except(other, this.Comparer).ToArray();
			foreach (var o in rm)
			{
				this._graph[o].Clear();
				this._graph.Remove(o);
			}
			foreach (var edges in this._graph.Values)
				edges.IntersectWith(other);
		}

		/// <summary>
		/// Intersects this graph with the designated graph, leaving only the vertices and edges that appear in other.
		/// Following completion, this.IsSubgraphOf(other) will be true.
		/// </summary>
		/// <param name="other"></param>
		public void IntersectGraphWith(Graph<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			var keys = new HashSet<T>(this._graph.Keys, this.Comparer);
			foreach (var kvp in other._graph)
			{
				keys.Remove(kvp.Key);
				var myedges = this._graph[kvp.Key];
				myedges.IntersectWith(kvp.Value);
			}
			foreach (var k in keys)
				this._graph.Remove(k);
		}

		/// <summary>
		/// Returns true if this graph contains only vertices that are in the collection, and that
		/// there is at least one vertex in the collection not in this graph (vertex proper subset).
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool IsProperSubsetOf(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			bool isproper = false;
			var keys = this._graph.Keys.ToDictionary((k) => k, (k) => false, this.Comparer);
			foreach (var o in other)
			{
				if (keys.ContainsKey(o))
					keys[o] = true;
				else
					isproper = true;
			}
			return isproper && keys.Values.All(Utility.AsIs);
		}

		/// <summary>
		/// Returns true if every vertex *and* edge that appears in this graph also appears in other, and at least one vertex and/or edge does not appear in
		/// this graph (graph proper subset).
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Subgraph")]
		public bool IsProperSubgraphOf(Graph<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			bool isproper = false;
			var keys = this._graph.Keys.ToDictionary((k) => k, (k) => false, this.Comparer);
			foreach (var kvp in other._graph)
			{
				if (keys.ContainsKey(kvp.Key))
				{
					var myedges = this._graph[kvp.Key];
					keys[kvp.Key] = true;
					if (!myedges.IsSubsetOf(kvp.Value))
						return false;
					isproper = isproper || kvp.Value.Any((v) => !myedges.Contains(v));
				}
				else
					isproper = true;
			}
			return isproper && keys.Values.All(Utility.AsIs);
		}

		/// <summary>
		/// Returns true if the collection contains only vertices that are in this graph, and that
		/// there is at least one vertex in this graph not in the colleciton (vertex proper superset).
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool IsProperSupersetOf(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			var keys = this._graph.Keys.ToDictionary((k) => k, (k) => true, this.Comparer);
			foreach (var o in other)
			{
				if (keys.ContainsKey(o))
					keys[o] = false;
				else
					return false;
			}
			return keys.Values.Any(Utility.AsIs);
		}

		/// <summary>
		/// Returns true if every vertex *and* edge that appears in the other graph also appears in this one, and at least one vertex and/or edge does not appear
		/// in the other graph (graph proper superset).
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Supergraph")]
		public bool IsProperSupergraphOf(Graph<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			var keys = this._graph.Keys.ToDictionary((k) => k, (k) => true, this.Comparer);
			foreach (var kvp in other._graph)
			{
				if (keys.ContainsKey(kvp.Key))
				{
					var myedges = this._graph[kvp.Key];
					if (!myedges.IsSupersetOf(kvp.Value))
						return false;
					if (myedges.SetEquals(kvp.Value))
						keys[kvp.Key] = false;
				}
				else
					return false;
			}
			return keys.Values.Any(Utility.AsIs);
		}

		/// <summary>
		/// Returns true if this graph contains only vertices that are in the collection (vertex subset).
		/// A graph is a subset of itself or of any graph equal to it.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool IsSubsetOf(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			var keys = new HashSet<T>(this._graph.Keys, this.Comparer);
			foreach (var o in other)
			{
				keys.Remove(o);
			}
			return keys.Count == 0;
		}

		/// <summary>
		/// Returns true if every vertex *and* edge that appears in this graph also appears in other (graph subset).
		/// A graph is a subset of itself or of any graph equal to it.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Subgraph")]
		public bool IsSubgraphOf(Graph<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			return other.IsSupergraphOf(this);
		}

		/// <summary>
		/// Returns true if the collection contains only vertices that are in this graph (vertex superset).
		/// A graph is a superset of itself or of any graph equal to it.
		/// 
		/// If other is a Graph&lt;T&gt;, then it redirects to the corresponding overload. If you want to query just vertices, retrieve the Vertices property.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool IsSupersetOf(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			foreach (var o in other)
			{
				if (!this._graph.ContainsKey(o))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Returns true if every vertex *and* edge that appears in the other graph also appears in this one (graph superset).
		/// A graph is a superset of itself or of any graph equal to it.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Supergraph")]
		public bool IsSupergraphOf(Graph<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			foreach (var kvp in other._graph)
			{
				if (!this._graph.ContainsKey(kvp.Key) || !this._graph[kvp.Key].IsSupersetOf(kvp.Value))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Returns true if any vertex in the collection appears also in this graph.
		/// There is no vertex/edge graph version because for the two graphs to share an edge they would necessarily share two vertices.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Overlaps(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			return other.Any((o) => this._graph.ContainsKey(o));
		}

		/// <summary>
		/// Removes the specified vertex from the graph. This also removes all edges incident on that vertex.
		/// </summary>
		/// <param name="item"></param>
		/// <returns></returns>
		public bool Remove(T item)
		{
			if (!this._graph.Remove(item))
				return false;
			foreach (var h in this._graph.Values)
				h.Remove(item);
			return true;
		}

		/// <summary>
		/// Returns true if this graph contains the same vertices as in the specified collection.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool SetEquals(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			var keys = this._graph.Keys.ToDictionary((k) => k, (k) => false, this.Comparer);
			foreach (var o in other)
			{
				if (keys.ContainsKey(o))
					keys[o] = true;
				else
					return false;
			}
			return keys.Values.All(Utility.AsIs);
		}

		/// <summary>
		/// Returns true if the target graph contains the same vertices and edges as this graph.
		/// </summary>
		/// <param name="other"></param>
		/// <returns></returns>
		public bool Equals(Graph<T> other)
		{
			if (other == null)
				return false;
			var keys = new HashSet<T>(this._graph.Keys, this.Comparer);
			foreach (var kvp in other._graph)
			{
				if (keys.Contains(kvp.Key))
				{
					if (!this._graph[kvp.Key].SetEquals(kvp.Value))
						return false;
					keys.Remove(kvp.Key);
				}
			}
			return keys.Count == 0;
		}

		/// <summary>
		/// Returns true if the target graph contains the same vertices and edges as this graph.
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
		{
			var g = obj as Graph<T>;
			if (g == null)
				return false;
			return Equals(g);
		}

		/// <summary>
		/// Computes a hash code for the Graph. No guarantee is made that the hash is in any way a good quality one. It is literally good enough to meet the
		/// requirement that two graphs that are Equals will have the same GetHashCode.
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			int hash = 0;
			foreach (var kvp in this._graph)
			{
				hash ^= this.Comparer.GetHashCode(kvp.Key);
				foreach (var val in kvp.Value)
					hash ^= this.Comparer.GetHashCode(val);
			}
			return hash;
		}

		/// <summary>
		/// Removes all vertices from this graph that appear in other and adds the ones that don't.
		/// Duplicate entries within other are ignored.
		/// Vertices that are removed will also remove all edges incident on that vertex.
		/// vertices added have no incident edges.
		/// </summary>
		/// <param name="other"></param>
		public void SymmetricExceptWith(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			var toAdd = new HashSet<T>(this.Comparer);
			var toRemove = new HashSet<T>(this.Comparer);
			foreach (var o in other)
			{
				if (Contains(o))
					toRemove.Add(o);
				else
					toAdd.Add(o);
			}
			this.RemoveVertices(toRemove);
			this.AddRange(toAdd);
		}

		/// <summary>
		/// Identical to the AddRange method.
		/// </summary>
		/// <param name="other"></param>
		void ISet<T>.UnionWith(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			AddRange(other);
		}

		/// <summary>
		/// Adds a collection of vertices to this graph. Duplicate vertices are ignored.
		/// The new vertices have no incident edges.
		/// </summary>
		/// <param name="other"></param>
		public void AddRange(IEnumerable<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			foreach (var o in other)
				Add(o);
		}

		/// <summary>
		/// Adds all of the vertices and edges of the specified graph to this graph.
		/// After completion, this.IsSupergraph(other) will be true.
		/// </summary>
		/// <param name="other"></param>
		public void GraphUnionWith(Graph<T> other)
		{
			if (other == null)
				throw new ArgumentNullException(nameof(other));
			foreach (var kvp in other._graph)
			{
				if (Contains(kvp.Key))
					this._graph[kvp.Key].UnionWith(kvp.Value);
				else
					this._graph.Add(kvp.Key, new HashSet<T>(kvp.Value, this.Comparer)); // Why are we not checking for vertices we don't have? Because we're adding those too!
			}
		}

		void ICollection<T>.Add(T item)
		{
			this.Add(item);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}

		/// <summary>
		/// Determine if a path exists from the vertex specified as "from" to the vertex specified as "to".
		/// True if and only if:
		/// IsAdjacent(from, to), or
		/// Pick an arbitrary vertex middle, TestReachable(from, middle) && TestReachable(middle, to)
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		public bool TestReachable(T from, T to)
		{
			if (!this._graph.ContainsKey(from))
				return false;
			var tried = new HashSet<T>(this.Comparer);
			var leftToTry = new HashSet<T>(this._graph[from], this.Comparer);
			while (leftToTry.Count > 0)
			{
				var which = leftToTry.First();
				tried.Add(which);
				leftToTry.Remove(which);
				if (this.Comparer.Equals(to, which))
					return true;
				leftToTry.UnionWith(this._graph[which]);
				leftToTry.ExceptWith(tried);
			}
			return false;
		}

		/// <summary>
		/// Test if this graph is undirected or rather, that every edge has a matching reverse edge.
		/// Vacuously true if there are no edges.
		/// </summary>
		/// <returns></returns>
		public bool IsUndirected()
		{
			foreach (var kvp in this._graph)
				foreach (var v in kvp.Value)
					if (!this._graph[v].Contains(kvp.Key))
						return false;
			return true;
		}

		/// <summary>
		/// Tests if the graph is regular: every vertex has the same number of edges.
		/// Vacuously true if there are no edges.
		/// </summary>
		/// <returns></returns>
		public bool IsRegular()
		{
			return this._graph.Select((kvp) => kvp.Value.Count).Distinct().Count() == 1;
		}

		/// <summary>
		/// Tests if the graph is complete: every vertex is adjacent to every other vertex. Loops are not considered in the check.
		/// </summary>
		/// <returns></returns>
		public bool IsComplete()
		{
			foreach (var kvp in this._graph)
			{
				if (!this._graph.Keys.All((k) => this.Comparer.Equals(k, kvp.Key) || kvp.Value.Contains(k)))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Fills in edges such that the current graph becomes complete.
		/// Specifically, an edge is added from every vertex to every other vertex.
		/// Any loops that existed in the graph before this operation are removed.
		/// A complete graph yields true from TestIsComplete.
		/// </summary>
		public void MakeComplete()
		{
			foreach (var kvp in this._graph)
			{
				kvp.Value.UnionWith(this._graph.Keys);
				kvp.Value.Remove(kvp.Key);
			}
		}
	}
}
