using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Router.Wpf.Abstractions;
using Router.Wpf.Abstractions.Routing;
using Router.Wpf.Core;

namespace Router.Wpf
{
    public class Indexer
    {
        public string Key { get; set; }
        public float Score { get; set; } = 1.0f;

        /// <summary>
        /// 是否在索引阶段对字段值切词。自由文本字段（路由 key、path）应为 true；
        /// 不可切的标识符（精确匹配）应为 false。在下方的内置索引器里，该开关
        /// 控制是否往切词桶里写 token —— 若为 false，则只把整个值作为一个 token
        /// 写到精确匹配桶。
        /// </summary>
        public bool IsTokenization { get; set; } = true;

        public Indexer(string key, float? score = null)
        {
            Key = key;
            if (score != null) Score = score.Value;
        }
    }

    public static class IndexerExtensions
    {
        // 反射结果缓存 —— 用 ConcurrentDictionary，从非 UI 线程注册路由时也是安全的。
        private static readonly ConcurrentDictionary<string, string[]> _propertyCache = new();
        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, PropertyInfo?>> _typeCache = new();

        private static object? GetNestedPropertyValue(this object obj, string propertyName)
        {
            var parts = _propertyCache.GetOrAdd(propertyName, n => n.Split('.'));

            object? data = obj;
            foreach (string part in parts)
            {
                if (data == null) return null;

                Type type = data.GetType();
                var properties = _typeCache.GetOrAdd(type, _ => new ConcurrentDictionary<string, PropertyInfo?>());
                var propInfo = properties.GetOrAdd(part, p => type.GetProperty(p));

                if (propInfo == null) return null;

                data = propInfo.GetValue(data, null);
            }
            return data;
        }

        public static Dictionary<Indexer, string> GetIndices(this RouteMatcher matcher, Indexer[] indexers)
        {
            var dic = new Dictionary<Indexer, string>();
            foreach (var indexer in indexers)
            {
                var value = matcher.GetNestedPropertyValue(indexer.Key);
                if (value != null && value.ToString() is string v)
                    dic.Add(indexer, v);
            }
            return dic;
        }
    }

    /// <summary>
    /// 内存式路由搜索索引。原先使用 Lucene.Net，但实际只用到：每文档字段存储、
    /// 前缀查询、一个简单的得分。Lucene 的核心特性（除小写 + 按非字母数字切词
    /// 之外的分词器、布尔查询、评分调优、段管理）我们一样没用上，~50 行的活
    /// 拖出 ~4MB 的依赖不划算，所以替换为内置实现。
    ///
    /// 新索引器：
    /// - 切词：小写化后按"连续非字母数字"切分。
    /// - 每字段维护一个 token → 桶 的映射。
    /// - 搜索时同样切词，逐个 query term 在所有字段桶里做前缀匹配，
    ///   按 <see cref="Indexer.Score"/> 字段权重加权累加。匹配比例
    ///   <c>qt.Length / token.Length</c> 作为打分倍率 —— 完整命中比
    ///   匹到更长 token 的得分高，自然排在前面。
    /// </summary>
    public class LuceneMemoryIndexer : IIndexer<RouteMatcher>, IDisposable
    {
        private static readonly Regex TermSplit = new(@"[^a-z0-9]+", RegexOptions.Compiled);

        private readonly Indexer[] _indexers;
        private readonly List<Document> _docs = new();
        private bool _disposed;

        private sealed class Document
        {
            public RouteMatcher Source = null!;
            // 原样存储用户传入的字段值（保留大小写），后续 Search 返回结果时回填到调用方。
            public Dictionary<string, string> Fields = new();
            // 每个字段的切词桶（已小写），搜索阶段用。
            public Dictionary<string, HashSet<string>> Tokens = new();
        }

        public LuceneMemoryIndexer(Indexer[] indexers)
        {
            _indexers = indexers;
        }

        public void Index(RouteMatcher[] routes)
        {
            ThrowIfDisposed();
            foreach (var r in routes) IndexOne(r);
        }

        public void Index(RouteMatcher matcher)
        {
            ThrowIfDisposed();
            IndexOne(matcher);
        }

        private void IndexOne(RouteMatcher matcher)
        {
            var doc = new Document { Source = matcher };
            foreach (var (indexer, value) in matcher.GetIndices(_indexers))
            {
                doc.Fields[indexer.Key] = value;
                if (indexer.IsTokenization)
                {
                    var tokens = Tokenize(value);
                    doc.Tokens[indexer.Key] = new HashSet<string>(tokens);
                }
                else
                {
                    // 精确匹配字段 —— 整个值小写化后作为单个 token 入桶。
                    doc.Tokens[indexer.Key] = new HashSet<string> { value.ToLowerInvariant() };
                }
            }
            _docs.Add(doc);
        }

        public List<NameValueCollection> Search(string queryText) => Search(queryText, 10);

        public List<NameValueCollection> Search(string queryText, int topN)
        {
            ThrowIfDisposed();
            var results = new List<NameValueCollection>();
            if (string.IsNullOrWhiteSpace(queryText) || topN <= 0) return results;

            var queryTerms = Tokenize(queryText);
            if (queryTerms.Length == 0) return results;

            // 对每篇文档打分：每个 query term 取它在所有字段中最高的加权前缀匹配分；
            // 把所有 query term 的最高分加起来作为这篇文档的总分。
            var scored = new List<(Document doc, double score)>(_docs.Count);
            foreach (var doc in _docs)
            {
                double total = 0;
                bool allTermsMatched = true;
                foreach (var qt in queryTerms)
                {
                    double bestForTerm = 0;
                    foreach (var indexer in _indexers)
                    {
                        if (!doc.Tokens.TryGetValue(indexer.Key, out var bucket)) continue;
                        foreach (var t in bucket)
                        {
                            if (!t.StartsWith(qt, StringComparison.Ordinal)) continue;
                            // 长度比：完全匹配为 1.0，索引 token 越长得分越低。
                            var ratio = (double)qt.Length / t.Length;
                            var weighted = ratio * indexer.Score;
                            if (weighted > bestForTerm) bestForTerm = weighted;
                        }
                    }
                    if (bestForTerm == 0)
                    {
                        allTermsMatched = false;
                        break;
                    }
                    total += bestForTerm;
                }

                if (allTermsMatched && total > 0)
                    scored.Add((doc, total));
            }

            return scored
                .OrderByDescending(s => s.score)
                .Take(topN)
                .Select(s =>
                {
                    var nv = new NameValueCollection();
                    foreach (var ix in _indexers)
                        nv.Add(ix.Key, s.doc.Fields.TryGetValue(ix.Key, out var v) ? v : null);
                    return nv;
                })
                .ToList();
        }

        private static string[] Tokenize(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
            return TermSplit.Split(s.ToLowerInvariant())
                            .Where(t => t.Length > 0)
                            .ToArray();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LuceneMemoryIndexer));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _docs.Clear();
        }
    }

    public class IndexRouteCollection : TypePathRouteCollection
    {
        private static readonly string KEY = $"{nameof(RouteMatcher.Route)}.{nameof(RouteMatcher.Route.Key)}";
        private static readonly string PATH = nameof(RouteMatcher.Path);

        private readonly Indexer[] _indexers =
        {
            new(KEY, 2.0f),
            new(PATH, 1.0f),
        };

        private readonly LuceneMemoryIndexer _indexer;

        public IndexRouteCollection()
        {
            _indexer = new LuceneMemoryIndexer(_indexers);
        }

        public override void Add(string pattern, Route route)
        {
            PathMatcher matcher = new TypePathParser(pattern);
            if (!Collections.TryGetValue(pattern, out var list))
            {
                list = new();
                Collections.Add(pattern, list);
            }
            var routeMatcher = new RouteMatcher(pattern, route, matcher);
            if (!routeMatcher.PathMatcher.IsPartial)
            {
                _indexer.Index(routeMatcher);
            }
            list.Add(routeMatcher);
        }

        public override IEnumerable<RouteSearchInfo> Search(string pattern) => Search(pattern, 10);

        public IEnumerable<RouteSearchInfo> Search(string pattern, int topN)
        {
            return _indexer.Search(pattern, topN)
                .Select(it => new RouteSearchInfo(it[KEY]!, it[PATH]!));
        }
    }
}
