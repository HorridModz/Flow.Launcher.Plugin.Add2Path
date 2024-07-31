using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace Flow.Launcher.Plugin.Add2Path
{

    static class ZipStringExtension
    {
        public static IEnumerable<List<char>> Zip(this string str1, string str2)
        {
            /* Zips two strings together, so that they may be iterated over.
            *  Wrapper for Enumerable.Zip() method; convenient because it converts strings to lists and specifies to return zipped element as an
            *  IEnumerable containing List<char>'s of the two chars for each index
            *  Returns an IEnumerable containing Lists of two chars each.
            *  The function can be called and iterated over like this:
            *  foreach (List<char> chars in Zip(str1, str2)
             {
                 char char1 = chars[0];         char char2 = chars[1];
             }
            */
            return str1.ToList().Zip(str2.ToList(), (char1, char2) => new List<char>() { char1, char2 });
        }
    }
}
