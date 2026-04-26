using System.Collections.Generic;

namespace Router.Wpf
{
    /// <summary>
    /// 纯字符串 URL 路径工具。替换掉原先的 <c>Path.Combine</c> + <c>Path.GetFullPath</c>
    /// + <c>ConvertToLinuxPath</c> 三件套 —— 那一组把 Windows 文件系统语义（盘符、反斜杠、
    /// <c>..</c> 越过根的处理）混进了纯路由场景，容易踩坑。
    /// </summary>
    internal static class PathUtil
    {
        /// <summary>
        /// 用单个 '/' 把 <paramref name="parent"/> 和 <paramref name="child"/> 拼起来，
        /// 修掉首尾多余斜杠；结果再走一遍 <see cref="Normalize"/>，使 '.' 和 '..' 折叠。
        /// </summary>
        public static string Combine(string? parent, string? child)
        {
            if (string.IsNullOrEmpty(parent)) return Normalize(child ?? string.Empty);
            if (string.IsNullOrEmpty(child))  return Normalize(parent);

            var combined = parent.TrimEnd('/') + "/" + child.TrimStart('/');
            return Normalize(combined);
        }

        /// <summary>
        /// 折叠 '.' 和 '..' 段，把连续的 '/' 合并成一个。前导 '/' 保留；越过根的
        /// '..' 被静默丢弃（不允许越级）。
        /// </summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/";

            var leading = path[0] == '/';
            var stack = new List<string>();
            var i = 0;
            var len = path.Length;
            while (i < len)
            {
                // 跳过连续的 '/'
                while (i < len && path[i] == '/') i++;
                if (i >= len) break;

                var start = i;
                while (i < len && path[i] != '/') i++;
                var seg = path.Substring(start, i - start);

                if (seg == ".") continue;
                if (seg == "..")
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                    continue;
                }
                stack.Add(seg);
            }

            var body = string.Join("/", stack);
            if (leading) return "/" + body;
            return body.Length == 0 ? string.Empty : body;
        }
    }
}
