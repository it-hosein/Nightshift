using UnityEngine;

namespace Nightshift
{
    static class MenuNavigation
    {
        public static int Move(int current, int count, int columns, int dx, int dy, int step)
        {
            if (count == 0) return -1;
            if (current < 0 || current >= count) return 0;
            if (step != 0) return (current + step + count) % count;
            if (dy == 0) return (current + dx + count) % count;
            int rows = (count + columns - 1) / columns;
            int row = (current / columns + dy + rows) % rows;
            return Mathf.Min(count - 1, row * columns + current % columns);
        }
    }
}
