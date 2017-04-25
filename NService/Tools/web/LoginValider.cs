using NService.Tools;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace NService
{
    public class LoginValider
    {
        //public static readonly LoginValider _instance = new LoginValider();

        public static LoginValider Instance
        {
            get
            {
                //return _instance;
                return ObjectFactory.Instance.Get<LoginValider>();
            }
        }

        protected virtual string validPwd(string account, string password)
        {
            //PCIService.WebPubService msg = new PCIService.WebPubService();
            //return msg.Login(account, password);
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add("user_nm", account);
            args.Add("usr_pas", password);
            args.Add("hashClient", null);
            DataSet ds = DBHelper.Instance.Query("Common_User_Login", args);
            if (ds == null || ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
            {
                return "0Account is not exists or password invalid!";
            }
            else
            {
                return "1" + ds.Tables[0].Rows[0]["user_id"];
            }
        }

        protected virtual string validUserID(string userID)
        {
            /// 可加入驗証策略
            /// 狀態是否正確(正常,未審核,已停用,已鎖定,三個月未改密碼,從未登錄過）
            return "1" + userID;
        }

        public virtual Dictionary<string, object> UserInfo(string userID)
        {
            Dictionary<string, object> args = new Dictionary<string, object>();
            args.Add("UserID", userID);
            return Tool.ToDic(DBHelper.Instance.Query("Common_User_Query", args));
        }

        public virtual string Valid(string account, string password)
        {
            string result = validPwd(account, password);
            string resultCode = result.Substring(0, 1);
            if (resultCode.Equals("1"))      //密碼驗証成功
                return validUserID(result.Substring(1));
            return result;
        }
    }

    public class RemoteLoginValider : LoginValider
    {
        string remoteLoginService;

        public RemoteLoginValider(string remoteLoginService)
        {
            this.remoteLoginService = remoteLoginService;
        }

        public override Dictionary<string, object> UserInfo(string userID)
        {
            return Tool.ToDic(ServiceCaller.Instance.Call(ServiceCaller.CallType.BaseCall, remoteLoginService, userID));
        }

        public override string Valid(string account, string password)
        {
            throw new NSInfoException("remote login only!");
        }
    }
}
