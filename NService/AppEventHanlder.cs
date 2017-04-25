using System;
using System.Collections;
using System.Collections.Generic;
using System.Web;
using System.Diagnostics;
using System.Reflection;
using NService.Tools;


namespace NService
{
    public class AppEventHanlder
    {
        public static readonly AppEventHanlder Instance = new AppEventHanlder();

        public DateTime AppStartTime
        {
            get
            {
                return _appStartTime;
            }
        }

        #region service變量設定(db臨時指定)
        /*
        public string ServiceVar(string varName)
        {
            if (HttpContext.Current != null && HttpContext.Current.Items.Contains("SERVICE_VAR"))
            {
                Dictionary<string, string> dic = HttpContext.Current.Items["SERVICE_VAR"] as Dictionary<string, string>;
                if (dic != null && dic.ContainsKey(varName))
                    return dic[varName];
            }
            return null;
        }

        public string SetServiceVar(string varName,string varValue)
        {
            if (HttpContext.Current != null)
            {
                Dictionary<string, string> dic = null;
                if (!HttpContext.Current.Items.Contains("SERVICE_VAR"))
                    HttpContext.Current.Items["SERVICE_VAR"] = dic = new Dictionary<string, string>();
                else
                    dic = HttpContext.Current.Items["SERVICE_VAR"] as Dictionary<string, string>;
                dic[varName] = varValue;
            }
            return null;
        }
        */
        public string ServiceVarContent()
        {
            if (HttpContext.Current != null && HttpContext.Current.Items.Contains("SERVICE_VAR_CONTENT"))
            {
                return HttpContext.Current.Items["SERVICE_VAR_CONTENT"] as string;
            }
            return null;
            /*
            暫時不加這個，有DEPLOY的設置一定是針對目標server的統一配置，而這個service可以將一些默認值hardcode在代碼中
            這樣開發時，就默認不用deploy選項，那些一定要的設定，可以通過constructor來override一定要config，并且設定好 
            string ret = null;
            if (HttpContext.Current != null && HttpContext.Current.Items.Contains("SERVICE_VAR_CONTENT"))
                ret = HttpContext.Current.Items["SERVICE_VAR_CONTENT"] as string;
            return (ret ?? "") + (ret != null && ret.Length > 0 ? "," : "") + "DEPLOY";
            */
        }


        public void SetServiceVarContent(string varContent)
        {
            if (HttpContext.Current != null)
            {
                //Dictionary<string, string> dic = new Dictionary<string, string>();
                Tool.Trace("set service var", "varContent", varContent??"null");
                //string[] args = varContent.Split(new char[] { '&' });
                //foreach (string arg in args)
                //{
                //    int argValueIndex = arg.IndexOf('=');
                //    string varName = arg.Substring(0, argValueIndex).Trim();
                //    string varValue = arg.Substring(argValueIndex + 1).Trim();
                //    dic[varName] = varValue;
                //}
                //HttpContext.Current.Items["SERVICE_VAR"] = dic;
                HttpContext.Current.Items["SERVICE_VAR_CONTENT"] = varContent;
                setConfigCodeInstallMap(varContent);
            }
        }

        void setConfigCodeInstallMap(string config)
        {
            //xxx$PCI.Install-Install:PCI.Install安裝在Install目錄中
            if (config != null && config.Length > 0 && config.IndexOf("-")>0)
            {
                string[] configs = config.Split(new char[] { ',' });
                foreach (string configItem in configs)
                {
                    if (configItem.IndexOf("-") >= 0)
                    {
                        HttpContext.Current.Items["SERVICE_CONFIG_CODE_INSTALL_MAP"] = configItem.Split(new char[] { '-' });
                        return;
                    }
                }
            }
            HttpContext.Current.Items["SERVICE_CONFIG_CODE_INSTALL_MAP"] = null;
        }

        public string GetInstallNameByHardCode(string configName)
        {
            if (HttpContext.Current != null && HttpContext.Current.Items.Contains("SERVICE_CONFIG_CODE_INSTALL_MAP"))
            {
                string[] codeInstallMap = HttpContext.Current.Items["SERVICE_CONFIG_CODE_INSTALL_MAP"] as string[];
                if (codeInstallMap != null && configName.StartsWith(codeInstallMap[0] + "."))
                    return codeInstallMap[1] + configName.Substring(codeInstallMap[0].Length);
            }
            return configName;
        }

        #endregion


        //手動結束app，往bin目錄下的寫一個文件，就會導致app重啟
        //有時需要手動結束app，如早上IIS會啟動memory回收機制，導致w3wp.exe暴力結束，而又沒有log，所以用這種方法在之前溫和的結束
        public void EndWebApp()
        {
            if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                Tool.Info("EndWebApp");
            System.IO.File.AppendAllText(HttpRuntime.BinDirectory + "\\EndWebApp.txt", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss fff") + " End Web Application!\r\n");
        }

        DateTime _appStartTime;

        public void OnStart()
        {
            _appStartTime = DateTime.Now;
            if(LogHelper.Instance.GetLogLevel()==LogHelper.LogLevel.High)
                Tool.Info("Application_Start");
        }

        public void OnEnd()
        {
            string shutDownMessage  = "";

            try{
                HttpRuntime runtime = (HttpRuntime)typeof(System.Web.HttpRuntime).InvokeMember("_theRuntime",
                    BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.GetField,
                    null,
                    null,
                    null);
                if (runtime != null)
                {
                        shutDownMessage = (string)runtime.GetType().InvokeMember("_shutDownMessage",
                            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField,
                            null,
                            runtime,
                            null);
                }
                else
                    shutDownMessage = "runtime 為null";
                /*
                string shutDownStack = (string)runtime.GetType().InvokeMember(
                    "_shutDownStack",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField,
                    null,
                    runtime,
                    null);
                */
            }
            catch(Exception ex){
                shutDownMessage = "獲取shutDownMessage出錯" + ex.Message;
            }

            TimeSpan ts = AppStartTime.Subtract(DateTime.Now);
            string timeStr = ts.TotalHours.ToString().Split(new char[] { '.' })[0] + "H" + ts.Minutes + "M" + ts.Seconds + "S";

            if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                Tool.Info("Application_End", "開始時間",AppStartTime.ToString("yyyy/MM/dd HH:mm:ss"),"總運行時間", timeStr,"總請求數",totalRequestNum, "Reason", shutDownMessage, "未完成的Rquest", requestNum, "在線日志", LogHelper.Instance.LogExList());
        }

        public string UserHost{
            get
            {
                return HttpContext.Current.Request.UserHostAddress;
            }
        }

        public string UserMachine
        {
            get
            {
                return HttpContext.Current.Request.UserHostName;
            }
        }

        public string RequestID
        {
            get
            {
                //Application_Start事件比OnRequest早
                object ret = HttpContext.Current.Items["_PCI_RequestID"];
                if(ret == null)
                    HttpContext.Current.Items["_PCI_RequestID"] = ret = Guid.NewGuid().ToString();
                return ret.ToString();
            }
        }

        public bool CanTrace
        {
            get
            {
                return HttpContext.Current ==null?false:HttpContext.Current.Request.QueryString["NoTraceHelperRecord"] != "1";
            }
        }

        public DateTime StartTime
        {
            get
            {
                if (HttpContext.Current == null)
                    return DateTime.Now;
                object ret = HttpContext.Current.Items["_PCI_StartTime"];
                if (ret == null)
                    HttpContext.Current.Items["_PCI_StartTime"] = ret = DateTime.Now;
                return (DateTime)ret;
            }
        }

        public DateTime EndTime
        {
            get
            {
                return (DateTime)HttpContext.Current.Items["_PCI_EndTime"];
            }
            private set
            {
                HttpContext.Current.Items["_PCI_EndTime"] = value;
            }
        }

        public void OnError()
        {
            //捕獲異常(ajax的異常可統一在ajax地方處理掉)
            Exception ex = HttpContext.Current.Server.GetLastError();
            if(ex is HttpException)
            {
                HttpContext.Current.Server.ClearError();
                return;
            }
            if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                Tool.Error("OnError Exception", "ex", ex.ToString());
            HttpContext.Current.Server.ClearError();
        }

        int requestNum = 0;
        int totalRequestNum = 0;
        //TimeSpan totalExecTime = new TimeSpan(0,0,0);

        public int TotalRequestNum
        {
            get
            {
                return totalRequestNum;
            }
        }

        public int RequestNum
        {
            get
            {
                return requestNum;
            }
        }

        //public TimeSpan TotalExecTime
        //{
        //    get
        //    {
        //        return totalExecTime;
        //    }
        //}

        static object _lockObj = new object();

        public void OnRequest()
        {
            DateTime now = this.StartTime;
            Tool.Trace("Begin Request");
            lock (_lockObj)
            {
                requestNum++;
                totalRequestNum++;
            }
            //使用Request.QueryString["SSOLOGONKEY"]來標識現在遠程登錄
            //SSOLoginHelper.Instance.RemoteLogin();      //遠程注入登錄(要提供WS供驗証，且這邊需配置服務表示接受遠程登錄服務)
            string userID = AuthenticateHelper.Instance.Authenticate(); //當前HttpContext.Current進行認証(識別出userID)
            LogHelper.Instance.StartRequest(now);


            //ServiceCaller.Instance.Call(ServiceCaller.CallType.BaseCall,"WebSocketServer.WebSocketServerTest.Send",AppEventHanlder.Instance.RequestID + "request " + HttpContext.Current.Request.Url.ToString() + ",from: " + HttpContext.Current.Request.UserHostAddress);
        }

        public void OnEndRequest()
        {
            DateTime now = DateTime.Now;
            this.EndTime = now;
            TimeSpan ts = now.Subtract(StartTime);
            //暫時沒時間去管這個警告了，Warn -> Trace，日志太多，等以后OK后再來打開這個，并且想辦法忽略一些情況
            //因為經常是稽核報表，Rec.Job，早上server剛啟動，第一次請求時超時
          
            if (ts.TotalSeconds >= 30)
                Tool.Trace("請求耗時太長(秒)", "花費(秒)", ts.TotalSeconds);
            lock (_lockObj)
            {
                requestNum--;
                //totalExecTime = totalExecTime.Add(ts);
            }
            LogHelper.Instance.EndRequest(ts.TotalMilliseconds);
            Tool.Trace("End Request","花費(毫秒)",ts.TotalMilliseconds);
            //ServiceCaller.Instance.Call(ServiceCaller.CallType.BaseCall, "WebSocketServer.WebSocketServerTest.Send", AppEventHanlder.Instance.RequestID + "request end,spend:" + ts.TotalMilliseconds + "毫秒");

        }
    }
}