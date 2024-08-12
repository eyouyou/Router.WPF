using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Router.WPF.Abstractions
{
    public interface IIndexer<T>
    {
        void Index(T[] data);
        void Index(T data);
        List<NameValueCollection> Search(string queryText);
    }

    public interface IIndexer<T, ResultT> : IIndexer<T>
    {
        List<ResultT> SearchParse(string queryText);
    }
}
