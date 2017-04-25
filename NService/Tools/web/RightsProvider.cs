using NService.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NService
{
    public class RightsProvider
    {
        public static RightsProvider Instance
        {
            get
            {
                return ObjectFactory.Instance.Get<DBRightsProvider>();//DBRightsProvider.Instance;// new RightsProvider();//
            }
        }

        public RightsProvider()
        {
            // _ttc = new FileConfig("RightsProvider", new TextTableParser());
        }

        public virtual bool HasServiceRight(string kind, string service, string command, string userIP, string userID)
        {
            return false;
        }
    }

    public class DBRightsProvider : RightsProvider
    {
        public new static readonly DBRightsProvider Instance = new DBRightsProvider();
        public DBRightsProvider()
        {
            //_cache = new HttpCache(this.GetType().FullName);
        }

        public override bool HasServiceRight(string kind, string service, string command, string userIP, string userID)
        {           
            if (userID != null)
            {
                //Check Disable User
                Dictionary<string, object> user = AuthenticateHelper.Instance.GetUserInfo(userID);
                if (user["disable_mk"].ToString() == "Y")
                    throw new NSErrorException("Account is disabled!!!");
            }
            
            string varContent = AppEventHanlder.Instance.ServiceVarContent();
            service = service + (varContent != null ? "$" + varContent : "");
            if (kind == "RightMenu") //check rights menu by user login
            {
                Dictionary<string, object> args = new Dictionary<string, object>();
                args.Add("LINK", service);
                args.Add("USERID", userID);
                System.Data.DataTable dt = DBHelper.Instance.Query("Nservice.Rights.Common_MenuUserRights", args).Tables[0];
                if(dt.Rows.Count>0)                
                    return true;
                else
                    throw new NSNoPermissionException(service + "." + command); 

            }
            else if (kind == "SupperAdmin") // check is supper admin
                if (HasSuperManager(userID))
                return true;
           
            return false;
        }

        public bool HasSuperManager(string userID)
        {
            if (userID != null)
            {
                ArrayList managers = ConfigTool<ArrayList>.Instance.getSystemConfig("AppSetup.Authority.SuperManager");               
                return managers.IndexOf(userID) >= 0;
            }
            return false;
        }

    }
}
