using System;
using Veldrid;

namespace Lanegam.Client
{
    public static class MappedResourceExtensions
    {
        public static unsafe Span<T> AsSpan<T>(this MappedResourceView<T> resource)
            where T : unmanaged
        {
            return new Span<T>((T*)resource.MappedResource.Data, resource.Count);
        }

        public static unsafe Span<T> AsSpan<T>(this MappedResourceView<T> resource, uint start)
            where T : unmanaged
        {
            if (start >= (uint)resource.Count)
                throw new ArgumentOutOfRangeException(nameof(start));

            return new Span<T>((T*)resource.MappedResource.Data + start, resource.Count - (int)start);
        }

        public static Span<T> AsSpan<T>(this MappedResourceView<T> resource, int start)
            where T : unmanaged
        {
            return resource.AsSpan((uint)start);
        }
    }
}
