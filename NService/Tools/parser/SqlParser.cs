using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NService.Tools
{
    public class SqlParser: IParser
    {
        public T Read<T>(string text) where T : class
        {
            if (typeof(T) == typeof(object) || typeof(T) == typeof(Dictionary<string, string>))
                return read(text) as T;
            throw new ApplicationException("SqlParser can only parse Dictionary<string,string>");
        }

        Dictionary<string, string> read(string text)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();        

            //多行注釋
            //text = Regex.Replace(text, @"/\*(.|\r|\n)*?\*/", "");
            text = Regex.Replace(text, @"/\*(.*\n)+?.*?\*/", "");      //.可以匹配\r
            

            //單行注釋
            text += "\r\n";     //保證最后一行是注釋時，下一個匹配能執行
            text = Regex.Replace(text, @"\r\n[ \t\v\f]*--.*(?=\r\n)", "\r\n");   //一行開頭，然後再空格，然後到\r\n，表示一行注釋
            //text = Regex.Replace(text, @"--.*\r\n", "");
            

            //dic資料填入
            TmpMatcher tm = new TmpMatcher(dic);
            MatchEvaluator me = new MatchEvaluator(tm.CapFunction);
            text = Regex.Replace(text, @".*?:.*", me);
            text = Regex.Replace(text, @"\r\n\s*\r\n", "\r\n");
            

            dic["Text"] = text;

            return dic;
        }

        class TmpMatcher
        {

            Dictionary<string, string> _dic;

            public TmpMatcher(Dictionary<string, string> dic)
            {
                _dic = dic;
            }

            public string CapFunction(Match match)
            {
                string mStr = match.ToString();
                string[] tmp = mStr.Split(new char[] { ':' });
                if (tmp[0].IndexOf("'") >= 0 || tmp[0].Trim().IndexOf(" ") >= 0)
                {
                    return mStr;
                }
                else
                {
                    _dic.Add(tmp[0], tmp[1].Replace("\r", "").Replace("\n", "").Trim());
                   
                    return "";
                }
            }
        }
    }
}
