using System;
using System.Text;
using System.Web;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;

namespace NService.Tools
{
    public class TraceHelper
    {

        public static string Format(object obj)
        {
            if (obj == null)
                return "null";
            else if (obj is IDictionary)
                return Format(obj as IDictionary);
            else if (obj is IList)
                return Format(obj as IList);
            else if (obj is IDictionaryEnumerator)
                return Format(obj as IDictionaryEnumerator);
            else if (obj is IEnumerator)
                return Format(obj as IEnumerator);
            else if (obj is DbCommand)
                return Format(obj as DbCommand);
            else if (obj is DataSet)
                return Format(obj as DataSet);
            else if (obj is DataTable)
                return Format(obj as DataTable);
            else if (obj is DataRow)
                return Format(obj as DataRow);
            return obj.ToString();
        }

        static string Format(DataSet ds)
        {
            return JsonConverter.Convert2Json(ds);           
        }

        static string Format(DataTable dt)
        {
            return JsonConverter.Convert2Json(dt);
            
        }

        static string Format(DataRow dr)
        {
            return JsonConverter.Convert2Json(dr);
        }

        sealed class DataUtils
        {
            public DataUtils() { }

            public static string ToString(DbType dbType, object val)
            {
                if (val == null || val == DBNull.Value)
                {
                    return "NULL";
                }

                Type type = val.GetType();

                if (dbType == DbType.AnsiString || dbType == DbType.AnsiStringFixedLength)
                {
                    return string.Format("'{0}'", val.ToString().Replace("'", "''"));
                }
                else if (dbType == DbType.String || dbType == DbType.StringFixedLength)
                {
                    return string.Format("N'{0}'", val.ToString().Replace("'", "''"));
                }
                else if (type == typeof(DateTime) || type == typeof(Guid))
                {
                    return string.Format("'{0}'", val);
                }
                else if (type == typeof(TimeSpan))
                {
                    DateTime baseTime = new DateTime(2006, 11, 23);
                    return string.Format("(CAST('{0}' AS datetime) - CAST('{1}' AS datetime))", baseTime + ((TimeSpan)val), baseTime);
                }
                else if (type == typeof(bool))
                {
                    return ((bool)val) ? "1" : "0";
                }
                else if (type == typeof(byte[]))
                {
                    return "[BINARY(" + ((byte[])val).Length + ")]";
                    //return "0x" + BitConverter.ToString((byte[])val).Replace("-", string.Empty);
                }
                else if (type.IsEnum)
                {
                    return Convert.ToInt32(val).ToString();
                }
                else if (type.IsValueType)
                {
                    return val.ToString();
                }
                else
                {
                    return string.Format("'{0}'", val.ToString().Replace("'", "''"));
                }
            }

            public static string ToString(System.Data.Common.DbCommand cmd)
            {
                if (cmd == null)
                {
                    return null;
                }

                string sql = cmd.CommandText;

                if (!string.IsNullOrEmpty(sql))
                {
                    System.Collections.IEnumerator en = cmd.Parameters.GetEnumerator();

                    while (en.MoveNext())
                    {
                        System.Data.Common.DbParameter p = (System.Data.Common.DbParameter)en.Current;
                        sql = sql.Replace(p.ParameterName, ToString(p.DbType, p.Value));
                    }
                }

                return sql.Replace("= NULL", "IS NULL");
            }
        }

        static string Format(DbCommand command)
        {
            StringBuilder sb = new StringBuilder();
            
            sb.Append("[DbCommand]\r\n");
            if (command.Connection != null)
                sb.Append("DB:" + command.Connection.Database + "(" + command.Connection.DataSource + ")\r\n");
            else
                sb.Append("DB:[Connection null]\r\n");
            sb.Append(string.Format("{0}\t{1}\t\r\n", command.CommandType, command.CommandText));
            if (command.Parameters != null && command.Parameters.Count > 0)
            {
                sb.Append("Parameters:\r\n");
                foreach (DbParameter p in command.Parameters)
                {
                    sb.Append(string.Format("{0}[{2}] = {1}\r\n", p.ParameterName, DataUtils.ToString(p.DbType, p.Value), p.DbType));
                }
            }
            sb.Append("\r\n");
            return sb.ToString();
        }
        
        static string Format(IDictionaryEnumerator args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[IDictionaryEnumerator]\r\n");
            while (args.MoveNext())
            {
                sb.AppendFormat("{0}:{1}\r\n", args.Key, Format(args.Value));
            }
            sb.Append("\r\n");
            return sb.ToString();
        }
        
        static string Format(IDictionary args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[IDictionary]\r\n");
            foreach (object key in args.Keys)
            {
                sb.AppendFormat("{0}:{1}\r\n", key, Format(args[key]));
            }
            sb.Append("\r\n");
            return sb.ToString();
        }
        
        static string Format(IList args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[IList]\r\n");
            foreach (object key in args)
            {
                sb.AppendFormat("{0}\r\n", Format(key));
            }
            sb.Append("\r\n");
            return sb.ToString();
        }
        
        static string Format(IEnumerator args)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[IEnumerator]\r\n");
            while (args.MoveNext())
                sb.AppendFormat("{0}\r\n", Format(args.Current));
            sb.Append("\r\n");
            return sb.ToString();
        }
        /*
        public static string FormatMsg3(string msg, params object[] args)
        {

            if (args != null && args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                    args[i] = Format(args[i]);
                msg = string.Format(msg, args);
            }
            return msg;

        }

        public static string FormatMsg(string msg, params object[] args)
        {
            try
            {
                //Dictionary<string,object> itemDic = new Dictionary<string,object>();
                StringBuilder sb = new StringBuilder();
                if (args != null && args.Length > 0)
                {
                    for (int i = 0; i < args.Length; i += 2)
                        sb.AppendFormat("{0}:{1}\r\n", args[i].ToString(), i + 1 < args.Length && args[i + 1] != null ? Format(args[i + 1]) : "null");
                        //itemDic.Add(args[i].ToString(), i + 1 < args.Length && args[i + 1] != null ?args[i + 1] : null);
                        //itemDic.Add();
                }
                string ret = msg + "\r\n" + sb.ToString();// Tool.ToJson(itemDic);
                return ret;
            }
            catch (Exception ex)
            {
                return "寫信息失敗:" + msg + "," + ex.Message;
            }
        }

        public static void AddWebContext(Dictionary<string, object> targs)
        {
            if (HttpContext.Current != null)
            {
                object userID = HttpContext.Current.Items.Contains("_AuthenticateHelper_UserID") ? HttpContext.Current.Items["_AuthenticateHelper_UserID"] : "";
                targs.Add("UserID", userID);

                string ip = HttpContext.Current.Request.UserHostAddress;
                targs.Add("IP", ip);

                object requestID = System.Web.HttpContext.Current.Items["_PCI_RequestID"] ?? "";
                targs.Add("RequestID", requestID.ToString());

                //不用Session了,全部是SessionLess 服務
                //System.Web.SessionState.HttpSessionState session = System.Web.HttpContext.Current.Session;
                //targs.Add("SessionID", session == null ? "" : session.SessionID);

                //只在Trace時使用
                //targs.Add("ProcessID",System.Diagnostics.Process.GetCurrentProcess().Id);
                //targs.Add("ThreadID", System.Threading.Thread.CurrentThread.ManagedThreadId);

                targs.Add("Url", HttpContext.Current.Request.Url.ToString());
            }
        }

        public static readonly TraceHelper _instance = new TraceHelper();

        public static TraceHelper Instance
        {
            get
            {
                return _instance;
            }
        }

        string _ip = null;

        public string IP
        {
            get
            {
                return _ip;
            }
            set
            {
                _ip = value;
            }
        }

        List<Dictionary<string, object>> _trace = new List<Dictionary<string, object>>();

        public List<Dictionary<string, object>> Items
        {
            get
            {
                return _trace;
            }
        }

        static int _seedID = 0;

        static object _lockObj = new object();

        public void Trace(Dictionary<string, object> args)
        {
            if (IP == null 
                || HttpContext.Current == null 
                || (IP != "All" && IP != HttpContext.Current.Request.UserHostAddress) 
                || HttpContext.Current.Request.QueryString["NoTrace"] == "1" 
                || HttpContext.Current.Request.Url.ToString().ToLower().Replace("/","\\").IndexOf("\\trace.aspx") > 0)
                return;

            lock (_lockObj)
            {
                args["ID"] = ++_seedID;
                _trace.Add(args);
            }
        }

        public void Trace(string msg, params object[] args)
        {
            if (IP == null
                || HttpContext.Current == null
                || (IP != "All" && IP != HttpContext.Current.Request.UserHostAddress)
                || HttpContext.Current.Request.QueryString["NoTrace"] == "1"
                || HttpContext.Current.Request.Url.ToString().ToLower().Replace("/", "\\").IndexOf("\\trace.aspx") > 0)
                return;
            DateTime now = DateTime.Now;
            msg = FormatMsg(msg, args);
            System.Diagnostics.StackTrace st = new System.Diagnostics.StackTrace(true);
            System.Diagnostics.StackFrame sf = st.GetFrame(2);
            string callMethod = sf.GetMethod().ReflectedType.Name + "." + sf.GetMethod().Name;
            //System.Diagnostics.ProcessThreadCollection threads = System.Diagnostics.Process.GetCurrentProcess().Threads;
            //string threadids = "";
            //foreach( System.Diagnostics.ProcessThread thread in threads)
            //    threadids += (threadids.Length>0?",":"") + thread.Id.ToString();
            Dictionary<string, object> targs = Tool.ToDic(new object[]{
                    "Type","Trace"
                    ,"Message",msg
                    ,"Time",now.ToString("yyyy/MM/dd HH:mm:ss fff")
                    ,"Method",callMethod
                    ,"ProcessID",System.Diagnostics.Process.GetCurrentProcess().Id
                    ,"ThreadID", System.Threading.Thread.CurrentThread.ManagedThreadId
                    //,"Threads",threadids
                    //,"Stack",System.Environment.StackTrace
                });
            AddWebContext(targs);
            Trace(targs);
        }
        */
    }
}