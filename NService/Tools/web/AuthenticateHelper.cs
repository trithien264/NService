using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Web;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using NService.Tools;


namespace NService
{
    //認証模組
    public interface IAuthenticateModule
    {
        string GetUserMemoryUserID();

        string GetUserMemoryUser();

        void SetUserIDMemory(string userID);

        void SetUserMemory(string userID, string loginInfo,double ExpiresDays);

        void ClearUserMemory();

    }

    public class SessionAuthenticateModule : IAuthenticateModule
    {
        public const string UserIDKey = "AuthenticateHelper_UserID";
        public const string UserKey = "AuthenticateHelper_User"; 
        public string GetUserMemoryUserID()
        {
            if(HttpContext.Current.Session!=null)
                return HttpContext.Current.Session["UserID"] as string;
            return null;
        }

        public string GetUserMemoryUser()
        {
            return "";
        }
        public void SetUserIDMemory(string userID)
        {
            if (HttpContext.Current.Session != null)
                HttpContext.Current.Session[UserIDKey] = userID;
        }

        public void SetUserMemory(string userID, string loginInfo, double ExpiresDays)
        {
            if (HttpContext.Current.Session != null)
                HttpContext.Current.Session[UserIDKey] = userID;
        }

        public void ClearUserMemory()
        {
            //HttpContext.Current.Session.Remove("UserID");
            if (HttpContext.Current.Session != null)
            {
                HttpContext.Current.Session.Clear();
                HttpContext.Current.Session.Abandon();
            }
        }
    }

    public class CookieAuthenticateModule : IAuthenticateModule
    {

        #region cookie加解密

        const string _KEY_64 = "a4G-8=Jk"; //必須是8個字符（64Bit)
        const string _IV_64 = "JKbN=5[?";  //必須是8個字符（64Bit)

        public static string Encrypt(string PlainText, string KEY_64, string IV_64)
        {
            byte[] byKey = System.Text.ASCIIEncoding.ASCII.GetBytes(KEY_64);
            byte[] byIV = System.Text.ASCIIEncoding.ASCII.GetBytes(IV_64);

            DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
            int i = cryptoProvider.KeySize;
            MemoryStream ms = new MemoryStream();
            CryptoStream cst = new CryptoStream(ms, cryptoProvider.CreateEncryptor(byKey, byIV), CryptoStreamMode.Write);

            StreamWriter sw = new StreamWriter(cst);
            sw.Write(PlainText);
            sw.Flush();
            cst.FlushFinalBlock();
            sw.Flush();
            return Convert.ToBase64String(ms.GetBuffer(), 0, (int)ms.Length);

        }

        public static string Decrypt(string CypherText, string KEY_64, string IV_64)
        {
            byte[] byKey = System.Text.ASCIIEncoding.ASCII.GetBytes(KEY_64);
            byte[] byIV = System.Text.ASCIIEncoding.ASCII.GetBytes(IV_64);

            byte[] byEnc;
            try
            {
                byEnc = Convert.FromBase64String(CypherText);
            }
            catch
            {
                return null;
            }

            DESCryptoServiceProvider cryptoProvider = new DESCryptoServiceProvider();
            MemoryStream ms = new MemoryStream(byEnc);
            CryptoStream cst = new CryptoStream(ms, cryptoProvider.CreateDecryptor(byKey, byIV), CryptoStreamMode.Read);
            StreamReader sr = new StreamReader(cst);
            return sr.ReadToEnd();
        }

        #endregion

        public const string LOGINCOOKIEKEY = "NServiceWebUserID";
        public const string LOGINUSERINFOCOOKIEKEY = "NServiceWebUserInfo";
        public const string AUTOLOGINCOOKIEKEY = "NServiceWebAutoLogin";

        public string GetUserMemoryUserID()
        {
            return GetUserMemoryByKey(LOGINCOOKIEKEY);
        }

        public string GetUserMemoryUser()
        {
            return GetUserMemoryByKey(LOGINUSERINFOCOOKIEKEY);
        }

        

        public string GetUserMemoryByKey(string key)
        {
            //記得還要加密哦
            HttpCookie cookie = HttpContext.Current.Request.Cookies[key];
            if (cookie != null && cookie.Value != null)
            {
                try
                {
                    string ret = Decrypt(cookie.Value, _KEY_64, _IV_64);
                    return ret;
                }
                catch(Exception ex)
                {                   
                    return null;
                }
            }          

            return null;
        }

        public string GetAutoLogin()
        {
            return GetUserMemoryByKey(AUTOLOGINCOOKIEKEY);
        }

        public void SetAutoLogin(string UserID)
        {
            HttpCookie AutologinCookie = new HttpCookie(AUTOLOGINCOOKIEKEY, Encrypt(UserID, _KEY_64, _IV_64));
            AutologinCookie.HttpOnly = true;
            AutologinCookie.Path = "/";
            HttpContext.Current.Response.AddHeader("P3P", "CP=CURa ADMa DEVa PSAo PSDo OUR BUS UNI PUR INT DEM STA PRE COM NAV OTC NOI DSP COR");
            HttpContext.Current.Response.Cookies.Add(AutologinCookie);
        }

        public void SetUserIDMemory(string userID)
        {
            try
            {
                userID = Encrypt(userID, _KEY_64, _IV_64);
            }
            catch
            {
                userID = null;
            }
            HttpCookie loginCookie = new HttpCookie(LOGINCOOKIEKEY, userID);
            loginCookie.HttpOnly = true;
            loginCookie.Path = "/";
            HttpContext.Current.Response.AddHeader("P3P", "CP=CURa ADMa DEVa PSAo PSDo OUR BUS UNI PUR INT DEM STA PRE COM NAV OTC NOI DSP COR");
            HttpContext.Current.Response.Cookies.Add(loginCookie);
        }

        public void SetUserMemory(string userID, string loginInfo, double ExpiresDays)
        {
            try
            {
                userID = Encrypt(userID, _KEY_64, _IV_64);
            }
            catch
            {
                userID = null;
            }
          
            HttpContext.Current.Response.AddHeader("P3P", "CP=CURa ADMa DEVa PSAo PSDo OUR BUS UNI PUR INT DEM STA PRE COM NAV OTC NOI DSP COR");

            if (loginInfo != null && loginInfo.Trim().Length > 0)
            {
                try
                {
                    loginInfo = Encrypt(loginInfo, _KEY_64, _IV_64);
                }
                catch
                {
                    loginInfo = null;
                }


                try
                {
                    HttpCookie loginCookieInfo = new HttpCookie(LOGINUSERINFOCOOKIEKEY, loginInfo);
                    loginCookieInfo.HttpOnly = true;
                    loginCookieInfo.Path = "/";
                    loginCookieInfo.Expires = DateTime.Now.AddDays(ExpiresDays);                 

                    HttpContext.Current.Response.Cookies.Add(loginCookieInfo);
                }
                catch (Exception)
                {
                    Tool.Error("Save cookie LOGINUSERINFOCOOKIEKEY error!!!");
                }
            }                 
        }

        public void ClearRememberUserMemory()
        {
            HttpCookie cookieInfo = HttpContext.Current.Request.Cookies[LOGINUSERINFOCOOKIEKEY];      
         
            if (cookieInfo != null)
            {
                cookieInfo.Expires = DateTime.Now.AddDays(-1);
                HttpContext.Current.Response.Cookies.Add(cookieInfo);
            }          
           
        }

        public void ClearUserMemory()
        {           
            HttpCookie cookieInfo = HttpContext.Current.Request.Cookies[LOGINUSERINFOCOOKIEKEY];
            HttpCookie cookieUserID = HttpContext.Current.Request.Cookies[LOGINCOOKIEKEY];
            HttpCookie cookieAutoLogin = HttpContext.Current.Request.Cookies[AUTOLOGINCOOKIEKEY];
            if (cookieInfo != null)
            {
                cookieInfo.Expires = DateTime.Now.AddDays(-1);
                HttpContext.Current.Response.Cookies.Add(cookieInfo);
            }

            if (cookieUserID != null)
            {
                cookieUserID.Expires = DateTime.Now.AddDays(-1);
                HttpContext.Current.Response.Cookies.Add(cookieUserID);
            }

            if (cookieAutoLogin != null)
            {
                cookieAutoLogin.Expires = DateTime.Now.AddDays(-1);
                HttpContext.Current.Response.Cookies.Add(cookieAutoLogin);
            }
        }

    }


    public class AuthenticateHelper
    {
        public static readonly AuthenticateHelper _instance = new AuthenticateHelper();

        static AuthenticateHelper()
        {
            //其實如果是隻有方法，沒有內部數據時，可以不注冊
            //ObjectFactory.Default.Register(_instance);
        }

        public static AuthenticateHelper Instance
        {
            get
            {
                return _instance;
            }
        }

        List<IAuthenticateModule> _authModules;

        public AuthenticateHelper()
        {
            _authModules = new List<IAuthenticateModule>();
            //因現在不用Session服務了
            //_authModules.Add(new SessionAuthenticateModule());     //用這個可以提高效率(避免每次cookie解密)
            _authModules.Add(new CookieAuthenticateModule());      //用這個可以避免Session過期
        }

        public const string UserIDKey = "_AuthenticateHelper_UserID";
        public const string UserKey = "_AuthenticateHelper_User";
        public const string UserInfoMethod = "UserInfo";

        public string UserID
        {
            get
            {
                return HttpContext.Current.Items[UserIDKey] as string;
                /*SessionAuthenticateModule ssAut = new SessionAuthenticateModule();
                return ssAut.GetUserMemoryUserID();*/
                //return HttpContext.Current.Session[UserIDKey] as string;
            }
        }

        public Dictionary<string, object> GetUserInfo(string userID)
        {
            Dictionary<string, object> ret = null;
            if (userID != null)
            {
                int lastIndex = userID.LastIndexOf('@');
                if (lastIndex > 0)
                {
                    object service = ObjectFactory.Instance.Get(userID.Substring(lastIndex + 1));
                    if (service != null)
                    {
                        System.Reflection.MethodInfo met = service.GetType().GetMethod(UserInfoMethod);
                        if (met != null)
                            ret = met.Invoke(service, new object[] { userID.Substring(0, lastIndex) }) as Dictionary<string, object>;
                        else
                            throw new ApplicationException("Service Must Have a UserInfo(string userID) Method(" + UserInfoMethod + ")");
                    }
                    Tool.Trace("service獲取User信息", "userID", userID);

                }
                else
                {
                    Dictionary<string, object> args = new Dictionary<string, object>();
                    args.Add("UserID", userID);
                    ret = Tool.ToDic(DBHelper.Instance.Query("Nservice.User.Common_Login_User", args));                  

                }
            }
            return ret;
        }

        public Dictionary<string, object> User
        {
            get
            {
                Dictionary<string, object> ret = null;
              
                if (HttpContext.Current.Items[UserKey] != null)
                {              
                    ret = HttpContext.Current.Items[UserKey] as Dictionary<string, object>;
                }
                else
                {
                    ret = GetUserInfo(UserID);
                    if (ret != null)
                        HttpContext.Current.Items[UserKey] = ret;
                }                
                return ret;
            }
        }
        
        void SetUserID(string userID)
        {
            HttpContext.Current.Items[UserIDKey] = userID;
            //HttpContext.Current.Session[UserIDKey] = userID;
            /*SessionAuthenticateModule ssAut = new SessionAuthenticateModule();
            ssAut.SetUserMemory(userID, "", 0);*/
        }

        public void LoginCookie()
        {

        }

       
      
        public void Login(string userID, bool? saveMemory=null)
        {
            bool bMem = saveMemory ?? false;
            if (bMem)
            {
                Dictionary<string, object> dicUser = GetUserInfo(userID);
                string loginInfo = "";
                if (dicUser != null && dicUser.Count > 0)
                {
                    loginInfo = Tool.ToJson(dicUser);
                }

                double ExpiresDays = 0;
                try
                {
                    ExpiresDays = double.Parse(dicUser["ExpiresDays"].ToString());
                }
                catch
                {
                }

                CookieAuthenticateModule cookieAut=new CookieAuthenticateModule();
                cookieAut.SetUserMemory(userID, loginInfo, ExpiresDays);
            }

            foreach (IAuthenticateModule authModule in _authModules)
                   authModule.SetUserIDMemory(userID);
            SetUserID(userID);            
        }

        public void Logout()
        {         
            foreach (IAuthenticateModule authModule in _authModules)
                authModule.ClearUserMemory();
            SetUserID(null);           
        }

        public string Authenticate()
        {
            string userID = null;
            foreach (IAuthenticateModule authModule in _authModules)
            {
                userID = authModule.GetUserMemoryUserID();
                if (userID != null)
                {
                    Tool.Trace("認証User", "authModule.GetType().FullName", authModule.GetType().FullName);
                    break;
                }
            }
            Tool.Trace("認証UserID", "UserID", userID == null ? "null" : userID);  
            SetUserID(userID);
            return userID;
        }


    }
}