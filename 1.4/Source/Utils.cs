using System.Collections.Generic;
using Verse;

namespace FasterGameLoading
{
    public static class Utils
    {
        public static T PopFirst<T>(this List<T> list)
        {
            T currentFirst = list[0];
            list.RemoveAt(0);
            return currentFirst;
        }
    }
}

