using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Web;
using System.Data;
using NService.Tools;
using System.Threading;

namespace NService
{

    public class HttpService
    {
        public static readonly HttpService Instance = new HttpService();

        public virtual void Run()
        {
            HttpContext context = HttpContext.Current;
            if (context != null)
            {
                int remain;
                string jsonCall = getJsonCall(context, out remain);
                if (jsonCall == null || jsonCall.Trim().Length == 0)
                    dealResult(Tool.ToDic(
                        new object[]{
                            "AjaxError",remain==0?"4":"7"
                            ,"Message",remain==0?"Must provide JsonService QueryString":"wait " +remain.ToString() + " package" }));
                else
                    dealResult(ServiceCaller.Instance.CallToDic(ServiceCaller.CallType.BaseCall, jsonCall));
            }
            else
                throw new ApplicationException("This method must be called through the http request");
        }

        public virtual void RunWs(string JsService)
        {    
            HttpContext context = HttpContext.Current;
            string jsonCall = JsService;
            if (jsonCall == null || jsonCall.Trim().Length == 0)
                dealResult(Tool.ToDic(
                    new object[]{
                            "AjaxError", "4"
                            ,"Message", "Must provide JsonService QueryString" }));
            else
                dealResult(ServiceCaller.Instance.CallToDic(ServiceCaller.CallType.PermissionCall, jsonCall));
        }


        protected virtual void dealResult(Dictionary<string, object> ret)
        {
            HttpContext.Current.Response.CacheControl = "no-cache";
            string noJsonError = HttpContext.Current.Request.QueryString["NoJsonError"];
            if (noJsonError != null)       //Do not json way output, if wrong, then directly enter the value of NoJson
            {
                HttpContext.Current.Response.Clear();
                if (ret["AjaxError"].ToString() == "0")
                {
                    HttpContext.Current.Response.Write(ret["Result"].ToString());
                }
                else
                {
                    if (noJsonError == "ServiceMessage")
                    {
                        //HttpContext.Current.Response.Write("AjaxError:" + ret["AjaxError"].ToString());
                        HttpContext.Current.Response.Write(ret["Message"].ToString());
                    }
                    else
                    {
                        HttpContext.Current.Response.Write(noJsonError);
                    }
                }
            }
            else
            {
                this.dealJsonResult(ret);
            }
        }

        protected virtual void dealJsonResult(Dictionary<string, object> ret)
        {
            DealJsonResult(HttpContext.Current, ret);
        }

        public void DealJsonResult(HttpContext context, Dictionary<string, object> ret)
        {
            ret["EndTimeTicks"] = Math.Round(DateTime.Now.Ticks / 10000M);
            string result = "";
            try
            {
                //throw new ApplicationException("Test");
                string callbackArgs = context.Request.QueryString["callbackArgs"];      //jsonp
                ret["callbackArgs"] = callbackArgs;
                result = Tool.ToJson(ret);
            }
            catch (Exception ex2)
            {
                if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                    Tool.Warn("The output json error", "ex2", ex2);
                //try again
                System.Threading.Thread.Sleep(1000);
                try
                {
                    //throw new ApplicationException("Test2");
                    result = Tool.ToJson(ret);

                }
                catch (Exception ex)
                {
                    Tool.Error("The output json error", "ex", ex);
                    result = "{\"AjaxError\":\"4\",\"Message\":\"Results Json mistakes:" + ex.Message + "\"}";
                }
            }
            context.Response.Clear();
            //When using iframe upload, IE will be a script to download
            string iframeRequest = context.Request.QueryString["iframeRequest"];      //jsonp
            if (iframeRequest != "1")
                context.Response.ContentType = "text/javascript; charset=utf-8";
            string callback = context.Request.QueryString["callback"];      //jsonp
            if (callback != null)
            {
                if (iframeRequest == "1")
                    context.Response.Write("<script type='text/javascript'>");
                if (callback.ToLower() == "sjs.run")
                {
                    context.Response.Write("sjs.run(function(){ return");
                }
                else
                {
                    context.Response.Write(callback);
                    context.Response.Write("(");
                }
            }
            context.Response.Write(result);
            if (callback != null)
            {
                if (callback == "sjs.run")
                    context.Response.Write("})");
                else
                    context.Response.Write(");");
                if (iframeRequest == "1")
                    context.Response.Write("</script>");
            }
        }



        string getJsonCall(HttpContext context, out int remain)
        {
            remain = 0;         //How many are left
            string jsonCall = context.Request["JsonService"]; //context.Request.QueryString["JsonService"];
            string jsonKey = context.Request["JsonKey"];
            if (jsonKey == null || jsonKey.Trim().Length == 0)
                return jsonCall;

            //Does not guarantee complete success
            string cacheKey = context.Request.UserHostAddress + "_" + jsonKey;          //IP
            int jsonSeq = int.Parse(context.Request["JsonSeq"]);
            int jsonTotal = int.Parse(context.Request["JsonTotal"]);
            string[] cacheJsonStr = null;

            cacheJsonStr = HttpRuntime.Cache[cacheKey] as string[];
            if (cacheJsonStr == null)
            {
                cacheJsonStr = new string[jsonTotal];
                for (int i = 0; i < cacheJsonStr.Length; i++)
                {
                    cacheJsonStr[i] = null;
                }
                HttpRuntime.Cache.Insert(cacheKey, cacheJsonStr
                    , null
                    , System.Web.Caching.Cache.NoAbsoluteExpiration
                    , new TimeSpan(0, 5, 0)         //5 minutes automatically deleted (that is 5 minutes to complete all the packet transmission)
                    , System.Web.Caching.CacheItemPriority.Default
                    , null
                );
            }

            cacheJsonStr[jsonSeq] = jsonCall;
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < cacheJsonStr.Length; i++)
            {
                if (cacheJsonStr[i] == null)
                {
                    remain++;
                }
                else if (remain == 0)
                {
                    sb.Append(cacheJsonStr[i]);
                }
            }
            if (remain == 0)
            {
                HttpRuntime.Cache.Remove(cacheKey);
                return sb.ToString();
            }
            else
            {
                return null;
            }
        }

    }


    public class ServiceCaller
    {
        public enum CallType
        {
            PermissionCall          //Including the rights, the service of the service call
            ,
            TransactionCall         //A service call is made only for the transaction
            ,
            BaseCall          //Only the most internal call (reflection call)
        }

        public static readonly ServiceCaller Instance = new ServiceCaller();


        public Dictionary<string, object> CallToDic(ServiceCaller.CallType callType, string jsonCall)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();

            object jsonObject = null;
            try
            {
                jsonObject = Tool.ToObject(jsonCall);
            }
            catch (Exception ex)
            {
                ret["AjaxError"] = "4";
                ret["Message"] = "Tool.ToObject failure,json format invalid(" + jsonCall + " message:" + ex.Message + ")";
                return ret;
            }

            bool isMultiple = false;     //Is not more than one call
            ArrayList items = null;
            if (jsonObject is ArrayList)
            {
                isMultiple = true;
                items = jsonObject as ArrayList;
            }
            else if (jsonObject is Dictionary<string, object>)
            {
                items = new ArrayList(new object[] { jsonObject });
            }

            List<Dictionary<string, object>> multipleRet = new List<Dictionary<string, object>>();
            if (items != null && items.Count > 0)
            {
                foreach (Dictionary<string, object> dic in items)
                    multipleRet.Add(CallToDic(callType, dic));
            }
            else
            {
                ret["AjaxError"] = "4";
                ret["Message"] = "service format(json) invalid(" + jsonCall + ")";
            }


            if (isMultiple)
            {
                ret["AjaxError"] = 0;
                ret["Result"] = multipleRet;
                ret["IsMultiple"] = true;
            }
            else
            {
                ret = multipleRet[0];
                ret["IsMultiple"] = false;
            }

            return ret;
            //return Tool.ToJson(ret);
        }


        public Dictionary<string, object> CallToDic(ServiceCaller.CallType callType, Dictionary<string, object> dic)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            if (dic != null)
            {
                string service = null;
                object[] args = null;
                // Together because the external service is not necessarily such a form, it may be a direct service name (such as PCIUserList)
                // So leave the interface here
                if (dic.ContainsKey("service"))
                {
                    service = dic["service"].ToString();
                    if (dic.ContainsKey("params"))
                    {
                        ArrayList tmp = dic["params"] as ArrayList;
                        if (tmp != null)
                        {
                            args = new object[tmp.Count];
                            tmp.CopyTo(args);
                        }
                    }
                    //Development test, you can specify the delay to test the network, database busy
                    if (dic.ContainsKey("callDelay"))
                        System.Threading.Thread.Sleep(int.Parse(dic["callDelay"].ToString()));

                    ret = CallToDic(callType, service, args);
                }
                else
                {
                    ret["AjaxError"] = 4;
                    ret["Message"] = "service call description invalid(must provide service!)";
                }
            }
            else
            {
                ret["AjaxError"] = 4;
                ret["Message"] = "service call description is null(not a json object)";
            }
            return ret;
        }


        public Dictionary<string, object> CallToDic(ServiceCaller.CallType callType, string service, params object[] args)
        {
            Dictionary<string, object> ret = new Dictionary<string, object>();
            try
            {
                object result = Call(callType, service, args);
                ret["AjaxError"] = 0;
                ret["Result"] = result;
                ret["Params"] = args;
            }            
            catch (NSNeedLoginException lex)
            {
                ret["AjaxError"] = 1;
                ret["Message"] = "Need Login(" + lex.Message + ")";

                if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                    Tool.Warn("Service call (login required)", "Info", lex.Message);
            }
            catch (NSNoPermissionException pex)
            {              
                ret["AjaxError"] = 2;
                ret["Message"] = "No Pemission:" + pex.Message;
                if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                    Tool.Warn("Service Invocation (No Privileges)", "Info", pex.Message);
            }
            catch (NSInfoException pex)
            {
                ret["AjaxError"] = 3;
                ret["Message"] =  pex.Message;
               
            }
            catch (ApplicationException aex)
            {
                ret["AjaxError"] = 4;
                ret["Message"] = aex.Message;
                Tool.Error("Application Exception", "Message", aex.Message, "ApplicationException", aex.InnerException);
            }     
            catch (Exception ex)
            {
                ret["AjaxError"] = 5;
                ret["Message"] = "[Unrecognized exception]: "+ ex.Message;
                Tool.Error("Service call error (uncaught)", "ex", ex);
            }

            ret["Service"] = service;
            return ret;
        }



        public object Call(CallType type, string service, params object[] args)
        {
            return call(type, service, args);
        }

        protected virtual object call(CallType type, string service, params object[] args)
        {
            try
            {
                int paramsIndex = service.IndexOf("$");
                if (paramsIndex > 0)
                {
                    AppEventHanlder.Instance.SetServiceVarContent(service.Substring(paramsIndex + 1));
                    service = service.Substring(0, paramsIndex);

                }
                // To PermissionCall as a new Session, so have to empty ServiceVarContent
                // multiple service batches call, the first does not affect the second
                else if (type == CallType.PermissionCall)
                {
                    AppEventHanlder.Instance.SetServiceVarContent(null);
                }


                int dotIndex = service.LastIndexOf(".");
                if (dotIndex <= 0 || dotIndex >= service.Length - 1)
                    throw new ApplicationException("Invalid service:" + service);
                string serviceId = service.Substring(0, dotIndex);
                string command = service.Substring(dotIndex + 1);
                //TODO: permissionCall should be allowed to judge the first authority, or the object has been instantiated
                object serviceObj = ObjectFactory.Instance.Get(serviceId);
                if (serviceObj == null)
                    throw new ApplicationException("Service not found:" + serviceId);


                if (type == CallType.PermissionCall)//"0")
                {
                    if (RightsAccessAttribute.Instance.HasRights(serviceObj, command))//Check Permission Attribute class and method                     
                         return transactionCall(serviceObj, serviceId, command, args);
                    else
                        return permissionCall(serviceObj, serviceId, command, args);
                }                   
                else if (type == CallType.TransactionCall)//"1")
                    return transactionCall(serviceObj, serviceId, command, args);
                else
                    return baseCall(serviceObj, serviceId, command, args);

          
            }
            finally
            {

            }
        }


        protected virtual object permissionCall(object serviceObj, string serviceId, string command, params object[] args)
        {
            string objKind = "Ajax";
          
            string objId = serviceId + "." + command;
            string userId = AuthenticateHelper.Instance.UserID;
            string userIP = AppEventHanlder.Instance.UserHost;


            if (DBRightsProvider.Instance.HasServiceRight("SupperAdmin", serviceId, command, userIP, userId) || DBRightsProvider.Instance.HasServiceRight("RightMenu", serviceId, command, userIP, userId))//UserInterFace || SupperAdmin || RightsMenu
            {
                return transactionCall(serviceObj, serviceId, command, args);
            }
            
            if (userId == null)
                throw new NSNeedLoginException(objKind + "." + objId);
            else
                throw new NSNoPermissionException(userId + ":" + objKind + "." + objId);

            
        }


        protected virtual object transactionCall(object serviceObj, string serviceId, string command, params object[] args)
        {
            DBHelper.Instance.SetDefaultTran();
            try
            {
                object ret = baseCall(serviceObj, serviceId, command, args);
                NService.DDD.DomainContext.Instace.Submit();
                //if (AppEventHanlder.Instance.UserHost != "172.19.6.86")
                DBHelper.Instance.CommitTran();
                //if (AppEventHanlder.Instance.UserHost == "172.19.6.86")
                //    DBHelper.Instance.RollbackTran();
                //DealOtherDeal();
                //2011.10.19 Changed to CommitTran implementation
                //if (HttpContext.Current.Items["__OTHERDEALS"] != null && HttpContext.Current.Items["__OTHERDEALS"].ToString() == "1")
                //    dealAllEx(false);    / / Start the implementation of processing order (already in the implementation will not be implemented)
                // If you just interrupt here, then the trouble? In particular, there are a lot of DealOtherDeal, may lose a lot
                // In addition there are problems in the order? Such as the first off error, the second off normal, it is possible that the program did not think the first customs clearance than the first?
                return ret;
            }
            catch
            {
                NService.DDD.DomainContext.Instace.Reset();
                DBHelper.Instance.RollbackTran();
                throw;
            }
        }


        protected virtual object baseCall(object serviceObj, string serviceId, string command, params object[] args)
        {
            try
            {

                if (serviceObj is IService)
                {
                    return (serviceObj as IService).Call(command, args);
                }
                else
                {
                    return serviceObj.GetType().InvokeMember(
                        command
                        , BindingFlags.Default | BindingFlags.InvokeMethod
                        , null
                        , serviceObj
                        , args);
                }
            }
            catch (TargetInvocationException tex)
            {
                Exception innerEx = tex.InnerException;
                if (innerEx is NSNeedLoginException
                    || innerEx is NSNoPermissionException
                    || innerEx is NSErrorException)
                    throw innerEx;

                else if (innerEx is NSInfoException)
                {
                    throw new NSInfoException(innerEx.Message.ToString(), innerEx);
                }
                else if (innerEx is ApplicationException)
                {
                    throw new ApplicationException("Message error: " + serviceId + "." + command + ":" + innerEx.InnerException.Message, innerEx.InnerException == null ? null : innerEx.InnerException);
                }
                else
                {
                    throw new ApplicationException("Message error: " + innerEx.Message.ToString(), innerEx);
                }


            }
            catch (MissingMethodException ex)
            {
                string argInfo = "";
                if (args != null)
                {
                    foreach (object arg in args)
                        argInfo += (argInfo.Length > 0 ? "," : "") + (arg == null ? "null" : arg.GetType().Name);
                }
                //Tool.Error("Exception Server.ServiceCaller.baseCall",ex.Message,"Params",argInfo);
                throw new ApplicationException("Exception Server.ServiceCaller.baseCall: " + ex.Message + "; Params:" + argInfo);
            }
        }

        #region Heterogeneous transaction processing (add value to PermissionCall to resolve different transactions)

        /*
          * Each service call, including a major transaction and other types of processing (such as other database exchange, file processing, mail, etc.)
          * After the main transaction successful commit, followed by the implementation of other types of processing
          * If it fails, it logs to the global domain so that the administrator can manually restart it
          * And record the original call
          */

        //Here is easy to write wrong, forget BatchID, so directly with the array, to remind the caller attention
        public void AddOtherDeal(string[] serviceInfo, params object[] args)
        {
            string service = serviceInfo[0];
            string batchID = serviceInfo[1];
            //string db = "Flow";
            //if (serviceInfo.Length > 2)
            //{
            // No way, can only be specified manually, because AddOtherDeal may be preceded by all the database implementation of the command first
            // // TODO: To prevent later designation of the database at AddOtherDeal
            // // The transaction ID used for AddOtherDeal can be compared to other transaction IDs
            //    db = serviceInfo[2];
            //    HttpContext.Current.Items["__OTHERDEALS_DB"] = db;
            //}
            Tool.Trace("Add asynchronous processing", "service", service, "batchID", batchID);//, "db", db);
            //HttpContext.Current.Items["__OTHERDEALS"] = "1";
            insertEx(service, args, batchID);//, db);
        }

        string exTable = "WEB_META_EXRECORD";

        public void DealOtherDeal(string db, string tranID)
        {
            //if (HttpContext.Current.Items["__OTHERDEALS"] != null && HttpContext.Current.Items["__OTHERDEALS"].ToString() == "1")
            //{
            db = db ?? "Flow";
            //string db = HttpContext.Current.Items["__OTHERDEALS_DB"] == null ? "Flow" : HttpContext.Current.Items["__OTHERDEALS_DB"].ToString();
            //HttpContext.Current.Items["__OTHERDEALS"] = "0";
            //TO DO：Here to order by, or else the same transaction, there are two identical batch, may be followed by the first to be caught out
            //OK.2012.5.24 The Sort property of the DataView is used
            DataSet ds = DBHelper.Instance.Query("Select_" + exTable + "@" + db, Tool.ToDic("REQUEST_ID", AppEventHanlder.Instance.RequestID, "TRAN_ID", tranID));
            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
            {
                ds.Tables[0].DefaultView.Sort = "EX_ID asc";
                DataView dv = ds.Tables[0].DefaultView;
                dv.Sort = "EX_ID asc";
                DataTable dt2 = dv.ToTable();
                Tool.Info("The batch transaction is started", "db", db, "tranID", tranID);
                foreach (DataRow dr in dt2.Rows)
                {
                    string retryInfo = retry(dr, db, tranID);
                    if (retryInfo.Length > 0)
                    {
                        if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                            Tool.Warn("The heterogeneous transaction after the transaction failed", "Info", retryInfo);
                    }

                }
                Tool.Info("Execution of the batch transaction is complete", "db", db, "tranID", tranID);
                //DealOtherDeal();        //Cycle, may Retry, there are AddOtherDeal Dongdong, so we should continue to deal with the cycle
            }
            //}
        }

        public string Retry(string exID, string db)
        {
            DBHelper.Instance.ClearDefaultTran();
            DataSet ds = DBHelper.Instance.Query("Select_" + exTable + "@" + db, Tool.ToDic(
                "EX_ID", exID
            ));
            if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count == 1)
            {
                DataRow dr = ds.Tables[0].Rows[0];
                string errMsg = retry(dr, db, "");
                if (errMsg.Length > 0)
                    throw new NSErrorException(errMsg);
                else
                    return "Retry succeeded";
            }
            else
                throw new NSErrorException("No (or more) exception handling was found:" + exID);
        }

        string retry(DataRow dr, string db, string tranID)
        {
            string exID = dr["EX_ID"].ToString();
            string batchID = dr["EX_BATCH_ID"].ToString();
            if (batchID.Length > 0)
            {
                DataSet ds2 = DBHelper.Instance.Query("Select_" + exTable + "@" + db, Tool.ToDic(
                    "EX_ID__L", exID     //Less than this ID
                    , "EX_BATCH_ID", batchID
                ));
                if (ds2 != null && ds2.Tables.Count > 0 && ds2.Tables[0].Rows.Count > 0)
                {
                    //Tool.Trace("The batch has an earlier Ex failure，And therefore can not be executed", "ExID", exID, "BatchID", batchID, "Count", ds2.Tables[0].Rows.Count
                    //    , "First ExID", ds2.Tables[0].Rows[0]["ExID"].ToString());
                    return "The batch has an earlier Ex failed to execute successfully and therefore can not be executed(BatchID:" + batchID + ",The number of:" + ds2.Tables[0].Rows.Count + ",First pen ID:" + ds2.Tables[0].Rows[0]["EX_ID"].ToString() + ")";
                }
            }

            string errMsg = "";
            try
            {
                retryEx(dr, db, tranID);
            }
            catch (Exception ex)
            {
                errMsg = "Retry failed:" + ex.ToString();// Message;
            }

            if (errMsg.Length == 0)
            {
                try
                {
                    //Tool.Trace("Start Deletes the heterogeneous processing execution sequence", "ExID", exID);
                    deleteEx(exID, db);
                    //Tool.Trace("End Deletes the heterogeneous processing execution sequence", "ExID", exID);
                    return "";
                }
                catch (Exception ex2)
                {
                    //Tool.Error("Retry successful, but delete failed (*! Please note *: please ignore or manually delete the data, or may be repeated!)", "exID", exID, "ex2", ex2);
                    return "Retry successful, but delete failed (*! Please note *: please ignore or manually delete the data, or may be repeated!):" + ex2.Message;
                }
            }
            else
            {
                try
                {
                    Tool.Trace("The update of the heterogeneous processing execution sequence is started", "ExID", exID);
                    updateEx(exID, errMsg, int.Parse(dr["TRY_COUNT"].ToString()), db);
                    Tool.Trace("Update heterogeneous processing execution sequence is completed", "ExID", exID);
                    return errMsg;
                }
                catch (Exception ex2)
                {
                    //Tool.Info("Retry failure, record failure (it does not matter, you can manually re-implementation)", "exID", exID, "ex2", ex2);
                    return "Retry failure, record failure (it does not matter, you can manually re-implementation):" + ex2.Message;
                }
            }
        }

        public string Ignore(string exID, string db)
        {
            try
            {
                deleteEx(exID, db);
                return "DeleteEx Success!!!";
            }
            catch (Exception ex)
            {
                throw new NSErrorException("Error deleteEx:" + ex.Message);     //Into a PCIBusException, said the user logic failure
            }
        }

        string retryEx(DataRow dr, string db, string tranID)
        {
            string exID = dr["EX_ID"].ToString();
            string service = dr["SERVICE"].ToString();
            string jsonArgs = dr["ARGS"].ToString();
            ArrayList argsList = Tool.ToList(jsonArgs);
            object[] args = null;
            if (argsList != null)
            {
                args = new object[argsList.Count];
                argsList.CopyTo(args);
            }
            //Tool.Info("The execution of the heterogeneous processing is started", "tranID", tranID, "ExID", exID, "service", service);
            updateExStart(exID, db);
            call(CallType.TransactionCall, service, args);
            //Tool.Info("The heterogeneous processing is finished", "tranID", tranID, "ExID", exID, "service", service);

            return exID;
        }

        void insertEx(string service, object[] args, string batchID)//, string db)
        {

            string exID = IdGenerator.Instance.NextNo("EX", "DealingException");
            string argsJson = Tool.ToJson(args);

            List<Dictionary<string, object>> otherDeals = HttpContext.Current.Items["__OTHERDEALS"] as List<Dictionary<string, object>>;
            if (otherDeals == null)
            {
                HttpContext.Current.Items["__OTHERDEALS"] = otherDeals = new List<Dictionary<string, object>>();
            }
            otherDeals.Add(Tool.ToDic(
                "EX_ID", exID
                , "EX_BATCH_ID", batchID      //Batch execution ID, the same ID must be executed in chronological order
                , "SERVICE", service
                , "ARGS", argsJson
                , "TRY_COUNT", 0          //0 has been executed several times
                , "EX_TIME", DateTime.Now.ToString("yyyyMMddHHmmss")
                , "EX_MSG", "待Job執行(Waiting Job Execute)"
                , "USER_ID", AuthenticateHelper.Instance.UserID
                , "REQUEST_IP", AppEventHanlder.Instance.UserHost
                , "REQUEST_ID", AppEventHanlder.Instance.RequestID
            ));

        }

        public void ClearEx()
        {
            HttpContext.Current.Items["__OTHERDEALS"] = null;
        }

        public bool HaveEx()
        {
            return HttpContext.Current.Items["__OTHERDEALS"] != null;
        }

        public bool SaveEx(string db, string tranID)
        {
            bool ret = false;
            List<Dictionary<string, object>> otherDeals = HttpContext.Current.Items["__OTHERDEALS"] as List<Dictionary<string, object>>;
            if (otherDeals != null && otherDeals.Count > 0)
            {
                if (tranID == null || tranID == "")
                    throw new ApplicationException("the implementation of the ClearDefaultTran, please check the code");
                ret = true;
                db = db ?? "Flow";      //The default repository Flow
                foreach (Dictionary<string, object> dic in otherDeals)
                {
                    dic["TRAN_ID"] = tranID;
                    DBHelper.Instance.Execute("Insert_" + exTable + "@" + db, dic);
                }
                HttpContext.Current.Items["__OTHERDEALS"] = null;
            }
            return ret;
            //HttpContext.Current.Items["__OTHERDEALS"] = "1";
        }

        void deleteEx(string exID, string db)
        {
            Dictionary<string, object> args = Tool.ToDic(
                "EX_ID", exID
            );
            DBHelper.Instance.NoUseDefaultTran(args);
            DBHelper.Instance.Execute("Delete_" + exTable + "@" + db, args);
        }

        void updateEx(string exID, string errInfo, int tryCount, string db)
        {
            Dictionary<string, object> args = Tool.ToDic(
                "EX_ID__W", exID
                , "TRY_TIME", DateTime.Now.ToString("yyyyMMddHHmmss")
                , "TRY_COUNT", tryCount + 1
                //The first error (may be the original implementation of the program error, and the error should be retried after the separation)
                , tryCount == 0 ? "EX_MSG" : "TRY_MSG", errInfo.Length > 200 ? errInfo.Substring(0, 190) : errInfo
            );
            DBHelper.Instance.NoUseDefaultTran(args);
            DBHelper.Instance.Execute("Update_" + exTable + "@" + db, args);
        }

        void updateExStart(string exID, string db)
        {
            Dictionary<string, object> args = Tool.ToDic(
                "EX_ID__W", exID
                , "TRY_TIME", DateTime.Now.ToString("yyyyMMddHHmmss")
                //The first error (may be the original implementation of the program error, and the error should be retried after the separation)
                , "TRY_MSG", "START_EXEC_TRAN_" + AppEventHanlder.Instance.RequestID
            );
            DBHelper.Instance.NoUseDefaultTran(args);
            DBHelper.Instance.Execute("Update_" + exTable + "@" + db, args);
        }

        #endregion

    }

    public interface IService
    {
        object Call(string command, object[] args);
    }
}