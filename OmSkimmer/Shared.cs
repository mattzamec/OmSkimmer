using System;

namespace OmSkimmer
{
    public static class Shared
    {
        [Flags]
        public enum Options
        {
            None = 0,
            Excel = 1,
            Sql = 2
        }
    }
}
