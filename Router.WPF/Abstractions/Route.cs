using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using Unity.UI.Core.Abstractions.Routing;

namespace Unity.UI.Core.Abstractions
{
    public class Route
    {
        public Route(string key, string path, IRouteHandler handler)
        {
            Key = key;
            Path = path;
            Handler = handler;
        }

        public string Key { get; set; }

        /// <summary>
        /// 路由路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 组件 包含窗体、视图、模态框等
        /// </summary>
        public IRouteHandler Handler { get; set; }
        /// <summary>
        /// 子路由
        /// </summary>
        public IList<Route>? Children { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Route route)
            {
                return Key == route.Key;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public Route CopyWith(IRouteHandler? handler = null)
        {
            Route clone = (Route)MemberwiseClone();
            if (handler != null)
            {
                clone.Handler = handler;
            }
            return clone;
        }
    }

    public class IndexRoute : Route
    {
        public IndexRoute(string key, IRouteHandler handler) : base(key, string.Empty, handler)
        {
        }
    }
}
