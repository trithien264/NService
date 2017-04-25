using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NService.Tools
{
    public class TextParser : IParser
    {
        public T Read<T>(string text) where T : class
        {
            if (typeof(T) == typeof(string) || typeof(T) == typeof(object))
                return read(text) as T;
            throw new ApplicationException("TextParser parse error");
        }

        string read(string text)
        {
            return text;
        }
    }
}
