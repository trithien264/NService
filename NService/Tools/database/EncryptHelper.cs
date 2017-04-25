using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;


namespace NService.Tools
{
    public class EncryptHelper
    {
        public string Encrypt(string from)
        {
            return CookieAuthenticateModule.Encrypt(from, _KEY_64, _IV_64);
        }

        const string _KEY_64 = "aX~-8@Jk"; //必須是8個字符（64Bit)
        const string _IV_64 = "b*2^）5[?";  //必須是8個字符（64Bit)

        public static string Decrypt(string from)
        {
            return CookieAuthenticateModule.Decrypt(from, _KEY_64, _IV_64);
        }

        const string DATABASE_CFG_PREFIX = "Database_";

        public void EncryptDB(string db)
        {
            FileConfig config = ((FileConfig)ObjectFactory.Instance.Config);
            string configName = DATABASE_CFG_PREFIX + db;
            Dictionary<string, object> cfg = config.Parse<Dictionary<string, object>>(configName);
            if (!cfg.ContainsKey("$ref"))
            {
                ArrayList args = cfg["Args"] as ArrayList;
                string type = args[0].ToString();
                if (!type.StartsWith("$"))
                {
                    args[0] = "$" + type;
                    args[1] = Encrypt(args[1].ToString());
                }
               
            }
        }

        public string DecryptDB(string db)
        {
            return DecryptDB(db, false);
        }

        public string DecryptDB(string db,bool save)
        {
            FileConfig config = ((FileConfig)ObjectFactory.Instance.Config);
            string configName = DATABASE_CFG_PREFIX + db;
            Dictionary<string, object> cfg = config.Parse<Dictionary<string, object>>(configName);
            if (cfg == null)
            {               
                return "null";
            }
            if (!cfg.ContainsKey("$ref"))
            {
                ArrayList args = cfg["Args"] as ArrayList;
                string type = args[0].ToString();
                string conn = args[1].ToString();
                
                return conn;
            }
            else
            {
                return DecryptDB(cfg["$ref"].ToString(), false);
            }
        }



    }
}