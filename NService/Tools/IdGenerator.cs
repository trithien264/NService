using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NService.Tools
{
    public class IdGenerator
    {
        public static readonly IdGenerator _instance = new IdGenerator();

        public static IdGenerator Instance
        {
            get
            {
                return _instance;
            }
        }


        Dictionary<string, int> _ids;
        Dictionary<string, string> _initTimes;
        private IdGenerator()
        {
            _ids = new Dictionary<string, int>();
            _initTimes = new Dictionary<string, string>();
        }

        /// <summary>
        /// 年月日轉成一碼
        /// </summary>
        /// <returns></returns>
        string getInitTime()
        {
            string prefix = DateTime.Now.ToString("yyyyMMddHHmmss");
            return prefix.Substring(2, 2) + simply(prefix.Substring(4, 2)) + simply(prefix.Substring(6, 2)) + simply(prefix.Substring(8, 2)) + prefix.Substring(10);
        }

        /// <summary>
        /// 2碼數字簡化(如01->1 ,09->9 ,10->A,11->B
        /// </summary>
        /// <param name="no"></param>
        /// <returns></returns>
        string simply(string no)
        {
            if (no.StartsWith("0"))
                return no.Substring(1);
            else
            {
                int number = int.Parse(no);
                return ((char)(number + 55)).ToString();
            }
        }

        string simply(int id)
        {
            string no = id.ToString();
            if (id < 10)
                no = "0" + id.ToString();
            return simply(no);

        }


        //10碼 年2月1日1時1分2秒2，種子1
        public string NextNo(string kind)
        {
            string ret = "";
            lock (_lockObj)
            {
                if (!_ids.ContainsKey(kind))
                {
                    int id = 1;
                    string seed = getInitTime();
                    _ids.Add(kind, id);
                    _initTimes.Add(kind, seed);
                    ret = seed + simply(id);
                }
                else
                {
                    int id = ++_ids[kind];
                    if (id > 35)
                    {
                        string seed = getInitTime();
                        if (seed == _initTimes[kind])
                        {
                            if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                                Tool.Info("Id Generator Sleep 1 second", "kind", kind, "seed", seed);
                            System.Threading.Thread.Sleep(1000);    //睡1秒，避免取到相同的時間種子(精確到秒)
                            seed = getInitTime();
                        }
                        id = 1;
                        _ids[kind] = id;
                        _initTimes[kind] = seed;
                        ret = seed + simply(id);
                    }
                    else
                        ret = _initTimes[kind] + simply(id);
                }
            }
            return ret;
        }

        static object _lockObj = new object();

        /// <param name="kind"></param>
        /// <param name="width">大于14碼</param>
        /// <returns></returns>
        public string NextNo(string prefix, string kind)
        {
            return prefix + NextNo(kind);
        }
    }
}
