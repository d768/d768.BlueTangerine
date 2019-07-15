using System.Collections.Generic;
using System.Linq;

namespace d768.BlueTangerine.Infrastructure.Extensions
{
    public static class EnumerableExtensions
    {
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable != null)
                return !enumerable.Any<T>();
            return true;
        }
    }
}