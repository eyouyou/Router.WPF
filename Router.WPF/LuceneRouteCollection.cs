using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Router.WPF.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.UI.Core;
using Unity.UI.Core.Abstractions;
using Unity.UI.Core.Abstractions.Routing;

namespace Unity.UI.WPF
{
    public class Indexer
    {
        public string Key { get; set; }
        public float Score { get; set; } = 1.0f;

        public bool IsTokenization { get; set; } = true;
        public Indexer(string key, float? score = null)
        {
            Key = key;
            if (score != null)
            {
                Score = score.Value;
            }
        }
    }

    public static class IndexerExtensions
    {
        static Dictionary<string, string[]> _propertyCache = new();
        static Dictionary<Type, Dictionary<string, PropertyInfo?>> _typeCache = new();
        static object? GetNestedPropertyValue(this object obj, string propertyName)
        {
            if (!_propertyCache.TryGetValue(propertyName, out var parts))
            {
                parts = propertyName.Split('.');
                _propertyCache.Add(propertyName, parts);
            }

            object? data = obj;
            foreach (string part in parts)
            {
                if (data == null)
                    return null;

                Type type = data.GetType();
                if (!_typeCache.TryGetValue(type, out var properties))
                {
                    properties = new();
                    _typeCache.Add(type, properties);
                }
                if (!properties.TryGetValue(part, out var propInfo))
                {
                    propInfo = type.GetProperty(part);
                    properties.Add(part, propInfo);
                }

                if (propInfo == null)
                    return null;

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
                {
                    dic.Add(indexer, v);
                }

            }

            return dic;
        }
    }

    public class LuceneMemoryIndexer: IIndexer<RouteMatcher>
    {
        private readonly LuceneVersion _version = LuceneVersion.LUCENE_48;
        private readonly RAMDirectory _indexDirectory;
        private readonly Indexer[] _indexers;
        public LuceneMemoryIndexer(Indexer[] indexers)
        {
            _indexDirectory = new RAMDirectory();
            _indexers = indexers;
        }

        public void Index(RouteMatcher[] routes)
        {
            using var analyzer = new StandardAnalyzer(_version);
            using var writer = new IndexWriter(_indexDirectory, new IndexWriterConfig(_version, analyzer));

            foreach (var route in routes)
            {
                IndexRoute(writer, route, _indexers);
            }
        }

        public void Index(RouteMatcher matcher)
        {
            using var analyzer = new StandardAnalyzer(_version);
            using var writer = new IndexWriter(_indexDirectory, new IndexWriterConfig(_version, analyzer));

            IndexRoute(writer, matcher, _indexers);
        }

        private static void IndexRoute(IndexWriter writer, RouteMatcher matcher, Indexer[] indexers)
        {
            var luceneDoc = new Document { };

            foreach (var (indexer, value) in matcher.GetIndices(indexers))
            {
                if (indexer.IsTokenization)
                {
                    var tokenizationField = new TextField(indexer.Key, value, Field.Store.YES)
                    {
                        Boost = indexer.Score
                    };
                    luceneDoc.Add(tokenizationField);
                }
                else
                {
                    var stringField = new StringField(indexer.Key, value, Field.Store.YES)
                    {
                        Boost = indexer.Score
                    };
                    luceneDoc.Add(stringField);
                }
            }

            writer.AddDocument(luceneDoc);
            writer.Commit();
        }

        public List<NameValueCollection> Search(string queryText)
        {
            using var analyzer = new StandardAnalyzer(_version);
            using var reader = DirectoryReader.Open(_indexDirectory);
            var searcher = new IndexSearcher(reader);

            var parser = new MultiFieldQueryParser(_version, _indexers.Select(it => it.Key).ToArray(), analyzer);
            var query = parser.Parse(queryText);
            var hits = searcher.Search(query, 10).ScoreDocs;

            var results = new List<NameValueCollection>();
            foreach (var hit in hits)
            {
                var doc = searcher.Doc(hit.Doc);
                var dic = new NameValueCollection();
                foreach (var item in _indexers)
                {
                    dic.Add(item.Key, doc.Get(item.Key));
                }
                results.Add(dic);
            }

            return results;
        }
    }

    public class IndexRouteCollection : TypePathRouteCollection
    {
        private static readonly string KEY = $"{nameof(RouteMatcher.Route)}.{nameof(RouteMatcher.Route.Key)}";
        private static readonly string PATH = nameof(RouteMatcher.Path);

        private readonly Indexer[] _indexers =
        {
            new Indexer(KEY, 2.0f),
            new Indexer(PATH, 1.0f)
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

        public override IEnumerable<RouteSearchInfo> Search(string pattern)
        {
            return _indexer.Search(pattern).Select(it => new RouteSearchInfo(it[KEY]!, it[PATH]!));
        }
    }
}
