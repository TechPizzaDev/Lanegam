using System;
using System.IO;
using System.Linq;

namespace Lanegam.Client
{
    internal static class AssetHelper
    {
        private static readonly string _assetRoot = Path.Combine(AppContext.BaseDirectory, "Assets");

        public static string GetPath(string assetPath)
        {
            return Path.Combine(_assetRoot, assetPath);
        }

        public static string GetPath(params string[] paths)
        {
            return Path.Combine(paths.Prepend(_assetRoot).ToArray());
        }
    }
}