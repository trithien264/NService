using System;
using System.Text;
using System.Web;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Globalization;
using System.Linq;

namespace NService.Tools
{
    public enum LogType
    {
        Trace,
        Info,
        Warn,
        Error
    }
    
    

    public class LogHelper
    {

        public static readonly LogHelper Instance = new LogHelper();

        Dictionary<string, object> noRequestBag;

        List<Dictionary<string, object>> logExList;

        public List<Dictionary<string, object>> LogExList()
        {
            return new List<Dictionary<string, object>>(logExList.AsReadOnly());
        }

      
        public enum LogLevel 
        {
            High, //FullLog
            Medium   //NoSystemLog
        };
        private LogLevel? _logLevel=null;      
        public LogLevel? GetLogLevel()
        {             
            try
            {
                if (string.IsNullOrEmpty(_logLevel.ToString()))//Has exists _logLevel
                {
                    string LogLevel = ConfigTool<string>.Instance.getSystemConfig("AppSetup.LogLevel");
                    _logLevel = (LogLevel)Enum.Parse(typeof(LogLevel), LogLevel , true);   
                }                    
            }
            catch (Exception)
            {
                _logLevel = LogLevel.High;
            }

            return _logLevel;
        }
        


        LogHelper()
        {
            noRequestBag = new Dictionary<string, object>();
            //TODO:除了Application_Start的這種，還有可能是static類，Timer控件的那種
            noRequestBag.Add("RequestID", Guid.NewGuid().ToString());
            noRequestBag.Add("Time", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss fff"));
            noRequestBag.Add("IP", "NONE");
            noRequestBag.Add("Items", new List<Dictionary<string, object>>());

            logExList = new List<Dictionary<string, object>>();
        }

        #region 最后300個追蹤信息

        Queue<Dictionary<string, object>> _traceQueue = new Queue<Dictionary<string, object>>();       //記錄最后100個訪問的Request

        public List<Dictionary<string, object>> TraceQueue()
        {
            return new List<Dictionary<string, object>>(_traceQueue);
        }

       

        #region traceToQueue
        static object _lockTraceObj = new object();
        public delegate void TraceEventHandler(Dictionary<string, object> evt);
        public TraceEventHandler OnTrace;

        void traceToQueue(Dictionary<string, object> evt)
        {
            lock (_lockTraceObj)
            {
                if (this.OnTrace != null)
                {
                    this.OnTrace(evt);
                }
                _traceQueue.Enqueue(evt);
                if (_traceQueue.Count > 300)
                {
                    _traceQueue.Dequeue();

                }
            }
        } 
        #endregion

        #region logToQueue
        static object _lockLogObj = new object();
        Queue<Dictionary<string, object>> _logQueue = new Queue<Dictionary<string, object>>();
        public delegate void LogEventHandler(Dictionary<string, object> evt);
        public LogEventHandler OnLog;

        void logToQueue(Dictionary<string, object> evt)
        {
            lock (_lockLogObj)
            {
                if (this.OnLog != null)
                {
                    this.OnLog(evt);
                }
                _logQueue.Enqueue(evt);
                if (_logQueue.Count > 300)
                {
                    _logQueue.Dequeue();

                }
            }
        }
        #endregion


        bool isTrace = false;

        public void EnableTrace()
        {
            isTrace = true;
        }

        public void DisableTrace()
        {
            isTrace = false;
            _traceQueue.Clear();
        }

        //Dictionary<string, Dictionary<string, object>> _currentRequests = new Dictionary<string, Dictionary<string, object>>();

        public void StartRequest(DateTime now)
        {
            //大于500,可能客戶端掛了，為防止服務器爆掉，因此闕值
            //string str=AppEventHanlder.Instance.RequestID;

            if (!isTrace || !AppEventHanlder.Instance.CanTrace)
                return;
            //lock (_lockTraceObj)
            //{
            //_currentRequests.Add(AppEventHanlder.Instance.RequestID, curTraceBag);
            traceToQueue(Tool.ToDic(
                 "RequestID", AppEventHanlder.Instance.RequestID
                , "Event", "StartRequest"
                , "StartTimeMs", Math.Round(now.Ticks / 10000M)      //1ms = 1000us = 1000 * 1000ns = 10 * 1000 ticks(1ticks = 100ns)
                , "QueryString", curTraceBag["QueryString"]
                , "InputStream", curTraceBag["InputStream"]
            ));



            //}
        }

        void onLevelUp(LogType type)
        {
            if (!isTrace || !AppEventHanlder.Instance.CanTrace)
                return;
            //lock (_lockTraceObj)
            //{
            traceToQueue(Tool.ToDic(
                "RequestID", AppEventHanlder.Instance.RequestID
                , "Event", "LevelUp"
                , "StartTimeMs", Math.Round(AppEventHanlder.Instance.StartTime.Ticks / 10000M)      //1ms = 1000us = 1000 * 1000ns = 10 * 1000 ticks(1ticks = 100ns)

                , "Level", (int)type
                , "QueryString", curTraceBag["QueryString"]
                , "InputStream", curTraceBag["InputStream"]

            ));
            //}
        }

        public void EndRequest(double totalTime)
        {
            curTraceBag["TotalTime"] = totalTime;
            string key = "LogType";
            Dictionary<string, object> dicEnd = new Dictionary<string, object>();
            if (curTraceBag.ContainsKey(key))
            {                
                dicEnd.Add("RequestID", AppEventHanlder.Instance.RequestID);
                dicEnd.Add("LogType", curTraceBag[key]);
                dicEnd.Add("TotalTime", totalTime);
               
                logToFile("EndRequest", string.Format("{0}==\r\n{1}==\r\n{2}"
                , AppEventHanlder.Instance.RequestID
                , curTraceBag[key]
                , totalTime), dicEnd);
               
               
            }

            try
            {
                //Write Log to DB
                foreach (Dictionary<string, object> dic in _logQueue)
                {
                    try
                    {
                        string beginFlag = dic["beginFlag"].ToString();
                        //"yyyy/MM/dd HH:mm:ss fff"
                        CultureInfo provider = CultureInfo.InvariantCulture;
                        dic["Time"] = DateTime.ParseExact(dic["Time"].ToString(), "yyyy/MM/dd HH:mm:ss fff", provider).ToString("yyyyMMddHHmmssfff");
                        if (beginFlag == "Request")
                        {
                            Dictionary<string, object> dicEndRequest = GetRequestEnd(_logQueue, dic["RequestID"].ToString());
                            dic["LogType"] = dicEndRequest["LogType"];
                            dic["TotalTime"] = dicEndRequest["TotalTime"];



                            DBHelper.Instance.NoLogResult(dic);
                            DBHelper.Instance.NoUseDefaultTran(dic);
                            DBHelper.Instance.Execute("NService.Util.Log.Insert_Meta_LogRequest", dic);
                        }
                        else if (beginFlag == "Item")
                        {

                            DBHelper.Instance.NoLogResult(dic);
                            DBHelper.Instance.NoUseDefaultTran(dic);
                            DBHelper.Instance.Execute("NService.Util.Log.Insert_Meta_Log", dic);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logQueue.Dequeue();
                        writeLogErr("EndRequest", ex.ToString(), "");
                    }
                }
            }
            catch (Exception ex)
            {
              
            }            
            _logQueue.Clear();


            if (!isTrace || !AppEventHanlder.Instance.CanTrace)
                return;
            //lock (_lockTraceObj)
            //{
            //放在trace中刪除，滿300個會刪除
            //_currentRequests.Remove(AppEventHanlder.Instance.RequestID);

            traceToQueue(Tool.ToDic(
                "RequestID", AppEventHanlder.Instance.RequestID
                , "Event", "EndRequest"
                , "StartTimeMs", Math.Round(AppEventHanlder.Instance.StartTime.Ticks / 10000M)      //1ms = 1000us = 1000 * 1000ns = 10 * 1000 ticks(1ticks = 100ns)
                , "Level", curTraceBag.ContainsKey("LogType") ? (int)(curTraceBag["LogType"]) : 0
                , "EndTimeMs", Math.Round(AppEventHanlder.Instance.EndTime.Ticks / 10000M)      //1ms = 1000us = 1000 * 1000ns = 10 * 1000 ticks(1ticks = 100ns)
                , "TotalTime", totalTime
                , "QueryString", curTraceBag["QueryString"]
                , "InputStream", curTraceBag["InputStream"]

            ));

          

            //}


        }

        private Dictionary<string,object> GetRequestEnd(Queue<Dictionary<string,object>> _logQueue,string RequestID)
        {
            var listLog = _logQueue.ToList();
            
            foreach(Dictionary<string,object> dic in listLog)
            {
                if (dic["beginFlag"].ToString() == "EndRequest" && dic["RequestID"].ToString() == RequestID)
                    return dic;
            }
            return null;
        }

        #endregion

        #region 最后200個有信息的Request

        Queue<Dictionary<string, object>> _queue = new Queue<Dictionary<string, object>>();       //記錄最后100個訪問的Request

        public List<Dictionary<string, object>> Queue()
        {
            //System.ArgumentException: 目的陣列不夠長。請檢查 destIndex 與長度，以及陣列的下限
            //？
            //return _queue.ToArray();
            List<Dictionary<string, object>> ret = new List<Dictionary<string, object>>(_queue);
            //if (!RightsProvider.Instance.HasDataRight("AdminLogView", "MustAllPermission"))        //不存在的權限，只有超級管理員才有
            //{
            string userHost = AppEventHanlder.Instance.UserHost;
            int count = ret.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                Dictionary<string, object> item = ret[i];
                if (ret[i]["IP"].ToString() != userHost)
                    ret.RemoveAt(i);
            }
            //}
            return ret;
        }

        #endregion

        #region 當前Request日志

        Dictionary<string, object> curTraceBag
        {
            get
            {
                if (HttpContext.Current != null)
                {
                    Dictionary<string, object> ret = null;
                    ret = HttpContext.Current.Items["_PCI_Trace"] as Dictionary<string, object>;
                    if (ret == null)
                    {
                        ret = new Dictionary<string, object>();
                        ret.Add("RequestID", AppEventHanlder.Instance.RequestID);
                        ret.Add("Items", new List<Dictionary<string, object>>());
                        HttpContext.Current.Items["_PCI_Trace"] = ret;
                        //如果上面到這里都出錯了，那只能寄希望于Application_Error
                        try
                        {
                            //儘量保證下一行語句每一行都成功
                            //有可能不成功的儘量放后面
                            ret.Add("Time", AppEventHanlder.Instance.StartTime.ToString("yyyy/MM/dd HH:mm:ss fff"));
                            ret.Add("TimeMS", Math.Round(AppEventHanlder.Instance.StartTime.Ticks / 10000M));
                            ret.Add("IP", AppEventHanlder.Instance.UserHost);
                            ret.Add("UserID", AuthenticateHelper.Instance.UserID);

                            //不能取Name，要不然可能會形成死循環log
                            //ret.Add("UserName", AuthenticateHelper.Instance.User != null ? AuthenticateHelper.Instance.User["Name"].ToString() : "");
                            ret.Add("Path", HttpContext.Current.Request.Url.ToString().Split(new char[] { '?' })[0]);
                            ret.Add("RefUrl", HttpContext.Current.Request.UrlReferrer == null ? "" : HttpContext.Current.Request.UrlReferrer.ToString());
                            string qs = HttpContext.Current.Request.Url.Query != null && HttpContext.Current.Request.Url.Query.Length > 1 ? HttpContext.Current.Server.UrlDecode(HttpContext.Current.Request.Url.Query.Substring(1)) : "";
                            ret.Add("QueryString", qs);
                            //ret.Add("Cookies", HttpContext.Current.Request.Cookies.ToString());
                            //ret.Add("Request.Form", HttpContext.Current.Request.Form.ToString());
                            if (HttpContext.Current.Request.Files.Count == 0)
                            {
                                try
                                {
                                    HttpContext.Current.Request.InputStream.Seek(0, SeekOrigin.Begin);
                                    string inputStream = (new StreamReader(HttpContext.Current.Request.InputStream)).ReadToEnd();
                                    ret.Add("InputStream", inputStream);
                                    HttpContext.Current.Request.InputStream.Seek(0, SeekOrigin.Begin);
                                }
                                catch (Exception ex)
                                {
                                    ret.Add("InputStream", "[InputStream Error]" + ex.Message);
                                }
                            }
                            else
                                ret.Add("InputStream", "");
                            /*
                            string requestBody = "";
                            //如果都沒有,則用RequestBody試試
                            if ((inputStream==null || inputStream.Trim().Length == 0) && (qs==null || qs.Trim().Length == 0))
                            {
                                string folderPath = System.AppDomain.CurrentDomain.BaseDirectory + "/App_Data/Log";
                                string filepath = folderPath + "/"
                                    + AppEventHanlder.Instance.RequestID + ".txt";
                                HttpContext.Current.Request.SaveAs(filepath, true);
                                requestBody = File.ReadAllText(filepath);
                                //File.Delete(filepath);
                            }
                            ret.Add("RequestBody", requestBody);
                            */
                        }
                        catch (Exception ex)
                        {
                            //這個欄位是varchar(max)
                            ret["InputStream"] = (!ret.ContainsKey("InputStream") ? "" : ret["InputStream"].ToString()) + " [!獲取curTraceBag出錯:" + ex.ToString() + "!]";
                        }
                    }
                    return ret;
                }
                else  //如修改web.config,導至系統結束，記錄Application_End信息時
                {
                    return noRequestBag;
                }
            }
        }

        string getBagItem(string key)
        {
            if (!curTraceBag.ContainsKey(key) || curTraceBag[key] == null)
                return "";
            else
                return curTraceBag[key].ToString();
        }

        #endregion

        #region 寫文件日志

        static object _lockObj = new object();

        void writeLogErr(string kind, string exMsg, string logMsg)
        {
            logExList.Add(Tool.ToDic(
                "Kind", kind
                , "LogError", exMsg
                , "Time", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss fff")
                , "LogMsg", logMsg
            ));
        }


        bool logToFile(string beginFlag, string msg, Dictionary<string,object> itemDB=null)
        {            
            try
            {
                Dictionary<string, object> dicAppSetup = ConfigTool<Dictionary<string, object>>.Instance.getSystemConfig("AppSetup");

                try
                {
                    if ((bool)dicAppSetup["AllowLogFile"])
                    {
                        string writemsg = String.Format(
                            "***[{0}]***\r\n{1}\r\n***[End]***\r\n"
                            , beginFlag
                            , msg
                        );

                        string folderPath = System.AppDomain.CurrentDomain.BaseDirectory + "/App_Data/Log/" + DateTime.Now.ToString("yyyyMM");
                        string filepath = folderPath + "/"
                            + DateTime.Now.ToString("yyyy-MM-dd-HH-") + (Math.Floor(DateTime.Now.Minute / 5d) * 5).ToString().PadLeft(2, '0') + ".txt";
                        lock (_lockObj)     //寫日志很頻繁
                        {
                            //throw new ApplicationException("測試Log異常");
                            if (!Directory.Exists(folderPath))
                                Directory.CreateDirectory(folderPath);
                            File.AppendAllText(filepath, writemsg, Encoding.UTF8);
                        }
                    }
                }
                catch (Exception ex)
                {
                    writeLogErr(beginFlag, ex.ToString(), msg);
                }

                

                //Write Log to Database
                if ((bool)dicAppSetup["AllowLogDB"])
                {
                    if(itemDB!=null)
                    {
                        if (!itemDB.ContainsKey("beginFlag"))
                            itemDB.Add("beginFlag", beginFlag);
                        logToQueue(itemDB);
                    }
                }

                
                return true;
            }
            catch (Exception ex)
            {
                //寫日志失敗
                //加入到logExList中
                writeLogErr(beginFlag, ex.ToString(), msg);
                //先記錄
                //先發Pcc Messenger
                //再發Mail
                //再發簡訊
                return false;
            }
        }

        bool writeRequest()
        {
            if (!curTraceBag.ContainsKey("_IsWrite"))
            {
                string RequestID=getBagItem("RequestID");
                string Time = getBagItem("Time");
                string IP = getBagItem("IP");
                string UserID = getBagItem("UserID");
                string Path = getBagItem("Path").Replace("\r\n", "\\r\\n");
                string RefUrl = getBagItem("RefUrl").Replace("\r\n", "\\r\\n");
                string QueryString = getBagItem("QueryString").Replace("\r\n", "\\r\\n");
                string InputStream = getBagItem("InputStream").Replace("\r\n", "\\r\\n");    
         
                Dictionary<string, object> dicLog = new Dictionary<string, object>();
                dicLog.Add("RequestID", RequestID);
                dicLog.Add("Time", Time);
                dicLog.Add("LogType", "");
                dicLog.Add("RequestIP", IP);
                dicLog.Add("UserID", UserID);
                dicLog.Add("Path", Path);
                dicLog.Add("RefUrl", RefUrl);
                dicLog.Add("QueryString", QueryString);
                dicLog.Add("InputStream", InputStream);
                dicLog.Add("TotalTime", 0);

                string info = String.Format(
                    "{0}==\r\n{1}==\r\n{2}==\r\n{3}==\r\n{4}==\r\n{5}==\r\n{6}==\r\n{7}"
                    , RequestID
                    , Time
                    , IP
                    , UserID
                    , Path
                    , RefUrl
                    , QueryString
                    , InputStream
                );

                bool ret = logToFile("Request", info, dicLog);      //Item不會補寫主檔（防止一直出錯，一直寫主檔）
                curTraceBag.Add("_IsWrite", ret);
                return ret;
            }
            return (bool)curTraceBag["_IsWrite"];
        }


        static object _lockNoRequestObj = new object();
        static object _lockNoRequestItemObj = new object();

        void writeLog(Dictionary<string, object> item)
        {
            if(!item.ContainsKey("RequestID"))
                item.Add("RequestID",getBagItem("RequestID"));
            string info = String.Format(
                    "{0}==\r\n{1}==\r\n{2}==\r\n{3}==\r\n{4}==\r\n{5}==\r\n{6}"
                    , getBagItem("RequestID")
                    , item["Seq"]
                    , item["Time"]
                    , item["Type"]
                    , item["Method"]
                    , item["Message"].ToString().Replace("\r\n", "\\r\\n")
                    , item["Args"]
                    );
            if (curTraceBag == noRequestBag)
            {                
                lock (_lockNoRequestObj)
                {
                    if (writeRequest())
                        logToFile("Item", info, item);
                    else        //主檔直接寫不進，明細只好直接記錄，不丟掉明細
                        writeLogErr("Item", "主檔寫入已失敗", info);
                }
            }
            else
            {
                if (writeRequest())
                    logToFile("Item", info, item);
                else        //主檔直接寫不進，明細只好直接記錄，不丟掉明細
                    writeLogErr("Item", "主檔寫入已失敗", info);
            }

        }

        #endregion

        #region 組織Log需要記錄的信息

        string formatArgs(params object[] args)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                if (args != null && args.Length > 0)
                {
                    for (int i = 0; i < args.Length; i += 2)
                        sb.AppendFormat("[#ARG#]{0}:{1}\r\n"
                            , args[i].ToString()
                            , i + 1 < args.Length && args[i + 1] != null ? TraceHelper.Format(args[i + 1]) : "null");
                }
            }
            catch (Exception ex)
            {
                sb.AppendFormat("[!Format異常:{0}!]", ex.Message);
            }
            return sb.ToString();
        }

        void log(LogType type, string msg, params object[] args)
        {
            DateTime now = DateTime.Now;
            string callMethod = "";
            string argsInfo = formatArgs(args);
            int processID = -1;
            int threadID = -1;
            try
            {
                System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(true);
                System.Diagnostics.StackFrame sf = st.GetFrame(3);      //1 is Warn,Error,Info Method
                callMethod = sf.GetMethod().ReflectedType.Name + "." + sf.GetMethod().Name;
            }
            catch (Exception ex)
            {
                callMethod = "[!ex:" + ex.Message + "!]";
            }
            try
            {
                processID = System.Diagnostics.Process.GetCurrentProcess().Id;
                threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            }
            catch
            {

            }

            Dictionary<string, object> item = Tool.ToDic(new object[]{
                "Type",type.ToString()
                ,"Message",msg
                ,"Args",argsInfo
                ,"Time",now.ToString("yyyy/MM/dd HH:mm:ss fff")
                ,"Method",callMethod
                , "ProcessID", processID
                , "ThreadID", threadID
            });
            List<Dictionary<string, object>> items = curTraceBag["Items"] as List<Dictionary<string, object>>;
            int seq;
            
            if (curTraceBag == noRequestBag)
            {
                lock (_lockNoRequestItemObj)
                {
                    seq = items.Count + 1;
                    item["Seq"] = seq;
                    items.Add(item);
                }
            }
            else
            {
                seq = items.Count + 1;
                item["Seq"] = seq;
                items.Add(item);
            }



            if (!curTraceBag.ContainsKey("_IsTrace"))
            {
                curTraceBag.Add("_IsTrace", true);

            }


            if (type > LogType.Trace)
            {
                writeLog(item);
                string key = "LogType";
                if (!curTraceBag.ContainsKey(key) || (int)type > (int)curTraceBag[key])
                {
                    curTraceBag[key] = type;
                    if (curTraceBag != noRequestBag)
                        onLevelUp(type);
                }
            }

        }

        #endregion

        #region 依據IP進行Trace

        string _ip = null;

        public void ClearTrace()
        {
            _ip = null;
        }

        public void TraceIP(string ip)
        {
            _ip = ip;
        }

        #endregion

        #region 程式調用進行日志和追蹤處理

        public void Trace(string msg, params object[] args)
        {
            if (_ip == null
                || HttpContext.Current == null          //因是按照IP追蹤，所以沒有Current，下面這個條件也不成立，所以不Trace
                || _ip != HttpContext.Current.Request.UserHostAddress
                || !AppEventHanlder.Instance.CanTrace
                )
                return;
            log(LogType.Trace, msg, args);
        }

        public void Info(string msg, params object[] args)
        {
            log(LogType.Info, msg, args);
        }

        public void Warn(string msg, params object[] args)
        {
            log(LogType.Warn, msg, args);
        }

        public void Error(string msg, params object[] args)
        {
            log(LogType.Error, msg, args);
        }

        #endregion

    }
}