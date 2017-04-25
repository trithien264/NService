using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NService.Tools
{
    public class AutoSqlConfig: FileConfig
    {
        public AutoSqlConfig(string subDir)          
        {
        }

        Dictionary<string, string> parse(string name)
        {
            /*
            Database:Database_WebPub

            Type:procedure

            dbo.pro_GetUserByNmAndPas_new
              
            Tables:shr_userd

            Select m.user_id UserID,user_desc Name,m.email Account
            ,ext Phone,end_mk Enable,last_date LastLogin,d.email Email
            FROM shr_userd m left join shr_usermail d on m.user_id = d.user_id
            Where m.user_id = *UserID*   
             * */


            name = name.Replace(".sql", "");
            Dictionary<string, string> ret = new Dictionary<string, string>();
            string[] tmp = name.Split(new char[] { '@' });
            bool isProcedure = tmp[0].Substring(0, 1) == "$";
            ret.Add("DB", tmp[1].IndexOf(".") < 0 ? "Database_" + tmp[1] : tmp[1]);
            ret.Add("Type", isProcedure ? "procedure" : "sql");
            if (isProcedure)
            {
                // ret.Add("Tables", tmp[0].Substring(1));
                ret.Add("Text", tmp[0].Substring(1));
            }
            else
            {
                string[] cmd = tmp[0].Split(new char[] { '_' }, 2);
                string textCmd = cmd[0];
                string table = cmd[1];
                //select默認不緩存(可通過$CACHE手動設定要緩存)，其它insert,update,delete都清空緩存 2012.12.13
                if (textCmd.ToLower() != "select" || table.EndsWith("$CACHE"))
                {
                    table = table.Replace("$CACHE", "");
                    ret.Add("Tables", table.Split(new char[] { '-' })[0]);      //加緩存，有一些table，如ERP的Brand,Vendorm可以直接Select_Brand@ERP,但是不能有緩存，因為增刪改不是在Web系統中完成
                }

                ret.Add("Text", "@" + textCmd + "(" + table + ")");      //隻能Insert,Update,Delete和Select，其中一般隻有Select直接用於客戶端
            }
            return ret;
        }

        public override T Parse<T>(string name)
        {
            //name中有空格表示直接是SQL在里面
           
            return base.Parse<T>(name);
        }

        public override T Parse<T>(string name, out string path)
        {
            T ret = base.Parse<T>(name, out path);
            if (ret == null && name.IndexOf("@") > 0)
                ret = parse(name) as T;
            return ret;
        }
    }
}
