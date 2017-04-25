using NService;
using NService.Tools;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Web;


namespace NService
{

    public class ClientTool
    {
        private static ClientTool _instance=null;
        public static ClientTool Instance
        {
            get { 
                if(_instance==null)
                    _instance = new ClientTool();
                return _instance;
            }
        } 

        public void RecordMenuAccess(int menuID)
        {
            HttpBrowserCapabilities bc = HttpContext.Current.Request.Browser;
            string refUrl = HttpContext.Current.Request.UrlReferrer == null ? "" : HttpContext.Current.Request.UrlReferrer.ToString();
            if (refUrl.Length > 500)
                refUrl = refUrl.Substring(0, 500);
            Dictionary<string, object> args = Tool.ToDic(
                                "USER_ID", AuthenticateHelper.Instance.UserID
                                , "MENU_ID", menuID
                                , "RECORD_TIME", DateTime.Now.ToString("yyyyMMddHHmmss")
                                , "USER_IP", AppEventHanlder.Instance.UserHost
                                , "REF_URL", refUrl
                                , "BROWSER", bc.Browser
                                , "VERSION", bc.Version
                                , "PLATFORM", bc.Platform
                                , "AGENT", HttpContext.Current.Request.UserAgent
                                );
            DBHelper.Instance.NoLogResult(args);
            DBHelper.Instance.NoUseDefaultTran(args);
            DBHelper.Instance.Execute("NService.Util.ClientTool.Insert_USER_MENU_RECORD", args);
        }

        public DataRowCollection getLogRequest(Dictionary<string, object> args)
        {
            return Tool.ToRows(DBHelper.Instance.Query("NService.Util.Log.getLogRequest", args));           
        }
    }

    public class ConfigTool<T>
    {
        private static ConfigTool<T> _instance = null;
        private static T _fileConfig;
        public static ConfigTool<T> Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ConfigTool<T>();
                return _instance;
            }
        } 

        public T getSystemConfig(string key)
        {
            T ret = default(T);
            if (_fileConfig != null)
                ret=_fileConfig;
            else
            {
                ret = SystemConfig.Instance.Get<T>(key);
                _fileConfig = ret;
            }
            return ret;
        }
    }


}