namespace Everything2Everything.Core.Providers;

/// <summary>
/// 모든 Provider의 변환 능력을 방향 그래프로 합성한다. 노드=확장자, 엣지=Provider+LossClass 가중치.
/// 자체 Dijkstra(외부 의존성 0, .NET PriorityQueue)로 임의의 입력→출력 멀티홉 경로를 최저 손실로 탐색한다.
/// 홉 제약(maxHops)이 있는 최단경로이므로 상태를 (노드, 홉수)로 두는 라벨링 Dijkstra를 쓴다.
/// (일반 Dijkstra는 hops 값이 '최저비용 경로의 홉'으로 오염돼 유효한 경로를 놓치는 false negative가 생긴다.)
/// </summary>
public sealed class ConversionGraph
{
    public readonly record struct Edge(string From, string To, IConverterProvider Provider, LossClass Loss)
    {
        public double Weight => LossWeights.Of(Loss) + LossWeights.HopPenalty;
    }

    private readonly Dictionary<string, List<Edge>> _adj = new(StringComparer.OrdinalIgnoreCase);

    public void AddEdge(string from, string to, IConverterProvider provider, LossClass loss)
    {
        var f = ConversionPair.Normalize(from);
        var t = ConversionPair.Normalize(to);
        if (!_adj.TryGetValue(f, out var list))
            _adj[f] = list = new List<Edge>();
        list.Add(new Edge(f, t, provider, loss));
    }

    public bool HasNode(string ext) => _adj.ContainsKey(ConversionPair.Normalize(ext));

    public IReadOnlyList<Edge> EdgesFrom(string ext)
        => _adj.TryGetValue(ConversionPair.Normalize(ext), out var list) ? list : Array.Empty<Edge>();

    /// <summary>
    /// 입력에서 도달 가능한 모든 출력 확장자 (홉 제약 도달성). FindBestPath와 동일한 (노드,홉) 도달 기준을 공유하므로
    /// "ReachableOutputs가 반환한 모든 X에 대해 FindBestPath(input, X) != null" 불변식이 성립한다.
    /// </summary>
    public IReadOnlyCollection<string> ReachableOutputs(string inputExt, int maxHops = 3, bool allowLossy = true)
    {
        var start = ConversionPair.Normalize(inputExt);
        if (maxHops < 1) maxHops = 1;
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<(string, int)>();
        var q = new Queue<(string Node, int Hop)>();
        q.Enqueue((start, 0));
        visited.Add((start, 0));

        // 동일포맷 self-edge(예: pdf→pdf 압축)도 도달 출력으로 노출
        foreach (var e in EdgesFrom(start))
            if (string.Equals(e.To, start, StringComparison.OrdinalIgnoreCase) && (allowLossy || e.Loss != LossClass.Rasterize))
                result.Add(e.To);

        while (q.Count > 0)
        {
            var (node, hop) = q.Dequeue();
            if (hop >= maxHops) continue;
            foreach (var e in EdgesFrom(node))
            {
                if (!allowLossy && e.Loss == LossClass.Rasterize) continue;
                if (!string.Equals(e.To, start, StringComparison.OrdinalIgnoreCase))
                    result.Add(e.To);
                var next = (e.To, hop + 1);
                if (visited.Add(next))
                    q.Enqueue(next);
            }
        }
        return result;
    }

    /// <summary>
    /// 입력→출력 최저 손실 경로 (홉 ≤ maxHops). 직접(1홉) 엣지가 있으면 홉 페널티로 거의 항상 최소가 된다.
    /// 동일포맷(start==goal)은 self-edge(압축 등)가 있을 때만 경로를 반환하고, 없으면 null(엔진이 Skip 처리).
    /// </summary>
    public IReadOnlyList<Edge>? FindBestPath(string inputExt, string outputExt, int maxHops = 3, bool allowLossy = true)
    {
        var start = ConversionPair.Normalize(inputExt);
        var goal = ConversionPair.Normalize(outputExt);
        if (maxHops < 1) maxHops = 1;

        if (string.Equals(start, goal, StringComparison.OrdinalIgnoreCase))
        {
            Edge? best = null;
            foreach (var e in EdgesFrom(start))
            {
                if (!string.Equals(e.To, goal, StringComparison.OrdinalIgnoreCase)) continue;
                if (!allowLossy && e.Loss == LossClass.Rasterize) continue;
                if (best is null || e.Weight < best.Value.Weight) best = e;
            }
            return best is null ? null : new[] { best.Value };
        }

        if (!_adj.ContainsKey(start)) return null;

        // 상태 = (노드, 홉수). 홉수를 상태에 포함해야 홉 제약 최단경로를 정확히 푼다.
        var dist = new Dictionary<(string Node, int Hop), double>();
        var prev = new Dictionary<(string Node, int Hop), (Edge Edge, string FromNode, int FromHop)>();
        var visited = new HashSet<(string, int)>();
        var pq = new PriorityQueue<(string Node, int Hop), double>();
        dist[(start, 0)] = 0;
        pq.Enqueue((start, 0), 0);

        (string Node, int Hop)? goalState = null;
        while (pq.TryDequeue(out var s, out var cost))
        {
            if (!visited.Add(s)) continue;
            if (string.Equals(s.Node, goal, StringComparison.OrdinalIgnoreCase)) { goalState = s; break; }
            if (s.Hop >= maxHops) continue;

            foreach (var e in EdgesFrom(s.Node))
            {
                if (!allowLossy && e.Loss == LossClass.Rasterize) continue;
                var next = (e.To, s.Hop + 1);
                var nd = cost + e.Weight;
                if (!dist.TryGetValue(next, out var c) || nd < c)
                {
                    dist[next] = nd;
                    prev[next] = (e, s.Node, s.Hop);
                    pq.Enqueue(next, nd);
                }
            }
        }

        if (goalState is null) return null;

        var path = new List<Edge>();
        var cur = goalState.Value;
        while (!(string.Equals(cur.Node, start, StringComparison.OrdinalIgnoreCase) && cur.Hop == 0))
        {
            var p = prev[cur];
            path.Add(p.Edge);
            cur = (p.FromNode, p.FromHop);
        }
        path.Reverse();
        return path;
    }
}
