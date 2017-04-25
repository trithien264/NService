using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NService.Tools
{
    public class AutoSqlConfig : FileConfig//AConfig
    {
        public AutoSqlConfig(string subDir): base(subDir, new SqlParser())
        {
        }
    }

    public class SqlHelper
    {
        static string cfgDir = "SqlHelper";        
        public static readonly SqlHelper Instance = new SqlHelper(new AutoSqlConfig(cfgDir));          
       

        FileConfig _config;
        public SqlHelper()
        {
        }
        public SqlHelper(FileConfig config)
        {
            _config = config;
        }

        public SqlHelper(string subDir, IParser parser)        
        {

        }

        string getCmdCfg(string cmdName, string key)
        {            
            Dictionary<string, string> tmp = _config.Parse<Dictionary<string, string>>(cmdName + ".sql");
            if (tmp != null && tmp.ContainsKey(key))
                return tmp[key];
            return null;
        }

        public Dictionary<string, string> getCmdCfgToDic(string cmdName)
        {           
            Dictionary<string, string> tmp = _config.Parse<Dictionary<string, string>>(cmdName + ".sql");
            if (tmp != null)
                return tmp;
            return null;
        }

        public string CommandType(string cmdName)
        {
            return getCmdCfg(cmdName, "Type");
        }
        
        public string CommandDb(string cmdName)
        {
            string ret = getCmdCfg(cmdName, "DB");           
            return ret;
        }


        public string CommandSql(string cmdText, Dictionary<string, object> args)
        {
            if (cmdText != null)
            {
                SqlReplaceHelper sr = new SqlReplaceHelper(cmdText, args);
                return sr.TranText();
            }
            else
                throw new ApplicationException("Command Text is null(cmd:" + cmdText + ")");
        }

      
    }

    internal class SqlReplaceHelper
    {
        Dictionary<string, object> _args;
        StringBuilder _textSb;

        public SqlReplaceHelper(string text, Dictionary<string, object> args)
        {
            _textSb = new StringBuilder(text);
            _args = args;
        }

        public string TranText()
        {
            replaceText();
            applyFunctions();
           
            return _textSb.ToString();
        }

        void replaceText()
        {
            if (_args != null)
            {
                foreach (string key in _args.Keys)
                {
                    string repKey = "#" + key + "#";
                    if (_args.ContainsKey(key) && _args[key] != null)
                        _textSb.Replace(repKey, _args[key].ToString());
                }
            }
        }

        void applyFunctions()
        {
            MatchEvaluator me = new MatchEvaluator(capFunction);
            //TODO:想辦法把中間的()自動匹配掉，這樣在寫sql時就不用在函數里使用{}來代替()了
            //或者簡單起見，就用[]或{}來代替()，看sql中出現哪種的機率小
            //@Insert(WEB_BD_REASON|ITEMNO,REASON,REASON_INDO)
            string text = Regex.Replace(_textSb.ToString(), @"@.*?\)", me);   //start with @(, and end with )    
            _textSb = new StringBuilder(text);
        }

        string capFunction(Match match)
        {
            string mStr = match.ToString();
            string functionText = mStr.Substring(1, mStr.Length - 1);        //-1,remove @
            string[] tmp = functionText.Trim().Split(new char[] { '(' });
            string name = tmp[0];
            if (tmp.Length == 2)
            {
                string paramStr = tmp[1].Substring(0, tmp[1].Length - 1);   //-1,remove)
                return SqlFunction.Instance.Call(name, paramStr, _args);
            }
            else
            {
                return mStr;
                //   throw new ApplicationException("Format is invalid:" + mStr);
            }
        }
       
    }
}
