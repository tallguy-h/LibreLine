// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Maps;

namespace LibreLine.Rules.Scoring;

internal sealed class UnionFind
{
    private readonly int[] parent;
    private readonly int[] size;

    public UnionFind(int count)
    {
        parent = Enumerable.Range(0, count).ToArray();
        size = Enumerable.Repeat(1, count).ToArray();
    }

    public int Find(int x)
    {
        while (parent[x] != x)
        {
            parent[x] = parent[parent[x]];
            x = parent[x];
        }

        return x;
    }

    public void Union(int a, int b)
    {
        var ra = Find(a);
        var rb = Find(b);
        if (ra == rb)
        {
            return;
        }

        if (size[ra] < size[rb])
        {
            (ra, rb) = (rb, ra);
        }

        parent[rb] = ra;
        size[ra] += size[rb];
    }

    public bool Connected(int a, int b) => Find(a) == Find(b);
}

/// <summary>Path questions about one player's routes.</summary>
public static class Graph
{
    /// <summary>The longest continuous path: each route counts once, cities may repeat.</summary>
    public static int LongestPath(IReadOnlyList<Route> routes)
    {
        var adjacency = Adjacency(routes);
        var used = new bool[routes.Count];
        var best = 0;
        foreach (var city in adjacency.Keys)
        {
            best = Math.Max(best, Walk(city, adjacency, routes, used));
        }

        return best;
    }

    private static int Walk(int city, Dictionary<int, List<int>> adjacency, IReadOnlyList<Route> routes, bool[] used)
    {
        var best = 0;
        foreach (var edge in adjacency[city])
        {
            if (used[edge])
            {
                continue;
            }

            used[edge] = true;
            var route = routes[edge];
            var next = route.From == city ? route.To : route.From;
            best = Math.Max(best, route.Length + Walk(next, adjacency, routes, used));
            used[edge] = false;
        }

        return best;
    }

    /// <summary>Asia: the number of cities in the player's largest connected network.</summary>
    public static int LargestNetwork(IEnumerable<Route> routes)
    {
        var list = routes.ToList();
        if (list.Count == 0)
        {
            return 0;
        }

        var cities = list.SelectMany(r => new[] { r.From, r.To }).Distinct().ToList();
        var index = cities.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
        var network = new UnionFind(cities.Count);
        foreach (var route in list)
        {
            network.Union(index[route.From], index[route.To]);
        }

        return cities.GroupBy(c => network.Find(index[c])).Max(g => g.Count());
    }

    /// <summary>How many paths sharing no route join two cities (counted up to <paramref name="limit"/>).</summary>
    public static int EdgeDisjointPaths(IReadOnlyList<Route> routes, int from, int to, int limit)
    {
        if (from == to)
        {
            return limit;
        }

        var adjacency = Adjacency(routes);
        if (!adjacency.ContainsKey(from) || !adjacency.ContainsKey(to))
        {
            return 0;
        }

        // Flow on each undirected route: +1 from From to To, -1 the other way.
        var flow = new int[routes.Count];
        var paths = 0;
        while (paths < limit)
        {
            var via = new Dictionary<int, int> { [from] = -1 };
            var queue = new Queue<int>();
            queue.Enqueue(from);
            while (queue.Count > 0 && !via.ContainsKey(to))
            {
                var city = queue.Dequeue();
                foreach (var edge in adjacency[city])
                {
                    var route = routes[edge];
                    var forward = route.From == city;
                    var next = forward ? route.To : route.From;
                    var residual = 1 - (forward ? flow[edge] : -flow[edge]);
                    if (residual > 0 && !via.ContainsKey(next))
                    {
                        via[next] = edge;
                        queue.Enqueue(next);
                    }
                }
            }

            if (!via.ContainsKey(to))
            {
                break;
            }

            var at = to;
            while (at != from)
            {
                var edge = via[at];
                var route = routes[edge];
                var forward = route.To == at;
                flow[edge] += forward ? 1 : -1;
                at = forward ? route.From : route.To;
            }

            paths++;
        }

        return paths;
    }

    private static Dictionary<int, List<int>> Adjacency(IReadOnlyList<Route> routes)
    {
        var adjacency = new Dictionary<int, List<int>>();
        for (var i = 0; i < routes.Count; i++)
        {
            foreach (var city in new[] { routes[i].From, routes[i].To })
            {
                if (!adjacency.TryGetValue(city, out var list))
                {
                    adjacency[city] = list = [];
                }

                list.Add(i);
            }
        }

        return adjacency;
    }
}
