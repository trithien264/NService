using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NService.Tools
{
    public class JsonParser: IParser
    {
        public T Read<T>(string text) where T : class
        {
            if (typeof(T) == typeof(object) || typeof(T) == typeof(Dictionary<string, object>))
                return read(text) as T;// Tool.ToDic(text) as T;
            throw new ApplicationException("JsonParser can only parse Dictionary<string,object>");
        }

        Dictionary<string, object> read(string text)
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();
           

            //多行注釋
            text = Regex.Replace(text, @"/\*(.|\r|\n)*?\*/", "");
          

            text += "\r\n";     //保證最后一行是注釋時，下一個匹配能執行
            //單行注釋
            text = Regex.Replace(text, @"\r\n[ \t\v\f]*\/\/.*(?=\r\n)", "\r\n");   //一行開頭，然後再空格，然後到\r\n，表示一行注釋
           

            //dic資料填入
            return Tool.ToDic(text);
        }
    }
}
