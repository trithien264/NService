using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Web;
using System.Data.OracleClient;
using System.Data.SqlClient;

namespace NService.Tools
{
    public class DBHelper
    {
        public static readonly DBHelper OrgInstance = new DBHelper();
        public static readonly DBHelper Instance = OrgInstance;


        #region Transaction

        public void SetDefaultTran()
        {
            HttpContext.Current.Items["__DEFAULTTRANID"] = DBHelper.Instance.BeginTran();
        }

        public void ClearDefaultTran()
        {
            string tranID = HttpContext.Current.Items["__DEFAULTTRANID"] as string;
            if (tranID != null)
                HttpContext.Current.Items.Remove("__DEFAULTTRANID");
        }


        public void CommitTran()
        {
            string tranID = HttpContext.Current.Items["__DEFAULTTRANID"] as string;
            string db = null;
            if (tranID != null)
            {
                if (HttpContext.Current.Items.Contains(tranID))
                {
                    Database tranDB = HttpContext.Current.Items["DB_" + tranID] as Database;
                    if (tranDB != null)
                    {
                        string dbName = ObjectFactory.Instance.GetObjectName(tranDB);
                        db = dbName.IndexOf(".") < 0 ? dbName.Substring("Database_".Length) : dbName;
                    }
                }
                //if (tranID == null)
                //    this.SetDefaultTran();      //Ensure that the back of the other deal is the transaction submitted
                //tranID = HttpContext.Current.Items["__DEFAULTTRANID"] as string;
                bool haveOtherDeal = ServiceCaller.Instance.SaveEx(db, tranID);
                if (tranID != null)
                    CommitTran(tranID);
                ClearDefaultTran();

                // After each transaction, it began to check with the main affairs of the heterogeneous treatment began to implement
                // To avoid as the Rec very slow transfer, not to start dealing with heterogeneous transactions
                // The new signing has started, leading to a large area of heterogeneous transaction delay

                if (haveOtherDeal)
                {
                    ServiceCaller.Instance.DealOtherDeal(db, tranID);
                }
            }
            else if (ServiceCaller.Instance.HaveEx())
            {
                if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                    Tool.Warn("A program with AddOtherDeal will not execute, please check if there is a TranID");
            }
        }

        public void RollbackTran()
        {
            string tranID = HttpContext.Current.Items["__DEFAULTTRANID"] as string;
            if (tranID != null)
                RollbackTran(tranID);
            ClearDefaultTran();
            ServiceCaller.Instance.ClearEx();

        }

        public string BeginTran()
        {
            return Guid.NewGuid().ToString();
        }

        public DbTransaction GetDefaultTran(Database db)
        {
            string tranID = HttpContext.Current.Items["__DEFAULTTRANID"] as string;
            if (tranID != null)
                return getTran(db, tranID, true);
            return null;
        }

        DbTransaction getTran(Database db, string tranID, bool isOpen)
        {
            DbTransaction tran = null;
            if (tranID != null)
            {
                if (HttpContext.Current == null)
                    return null;//throw new ApplicationException("Get Tran Failure,HttpContext.Current is null");
                else
                {
                    if (HttpContext.Current.Items.Contains(tranID))
                    {
                        //Only one transaction can be executed at a time
                        tran = HttpContext.Current.Items[tranID] as DbTransaction;
                        Database tranDB = HttpContext.Current.Items["DB_" + tranID] as Database;
                        if (tran != null && tranDB != null && db != null && (tranDB == db || tranDB.IsSameConnection(db)))
                            return tran;
                        else
                        {
                            // Because RequestID (cef2ba6c-304a-48fe-8451-612728746668) of the middle of the transaction there is no transaction situation
                            // So add these logs to determine when such a situation in the future analysis

                            if (isOpen)     //Only Execute, not caught Tran, only need Log
                            {
                                if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                                {
                                    if (tran == null)
                                        Tool.Warn("Remove DbTransaction from HttpContext.Current.Items as null", "tranID", tranID);
                                    else if (tranDB == null || db == null)
                                        Tool.Warn("DB null", "tranDB", tranDB == null ? "null" : "OK", "db", db == null ? "null" : "OK");
                                    else if (!tranDB.IsSameConnection(db))
                                        Tool.Warn("TranDB and db is not the same connection string, the programmer to check the program, with a different transaction to resolve different DB transaction processing", "tranDB ID", ObjectFactory.Instance.GetObjectName(tranDB), "db ID", ObjectFactory.Instance.GetObjectName(db));
                                    else if (tranDB != db)      //This situation should not happen again, the code is to put this bar
                                        Tool.Warn("DB is not the same, the ObjectFactory may be taken to two examples", "tranDB ID", ObjectFactory.Instance.GetObjectName(tranDB), "db ID", ObjectFactory.Instance.GetObjectName(db));
                                }
                            }
                            return null;// throw new ApplicationException("Get Tran Failure,tranID is not a DbTransaction!(" + tranID + ")");
                        }
                    }
                    else if (isOpen)
                    {
                        tran = db.BeginTransaction();
                        HttpContext.Current.Items.Add(tranID, tran);
                        HttpContext.Current.Items.Add("DB_" + tranID, db);
                        Tool.Info("Begin Transaction", "tranID", tranID);
                    }
                }
            }
            return tran;
        }

        /// <summary>
        /// After the transaction can be stored in a single class address management, so that web applications and windows programs can share this class (currently not)
        /// </summary>
        /// <param name="tranID"></param>
        public void CommitTran(string tranID)
        {
            try
            {
                //throw new ApplicationException("[Commit]Get Tran Failure,HttpContext.Current is null");
                if (HttpContext.Current != null)
                {
                    if (HttpContext.Current.Items.Contains(tranID))
                    {
                        DbTransaction tran = HttpContext.Current.Items[tranID] as DbTransaction;
                        if (tran != null)
                        {
                            DbConnection conn = tran.Connection;
                            tran.Commit();
                            if (conn != null)
                                conn.Close();
                            HttpContext.Current.Items.Remove(tranID);
                            HttpContext.Current.Items.Remove("DB_" + tranID);

                            Tool.Info("Commit Transaction", "tranID", tranID);
                        }
                        //else
                        //    throw new ApplicationException("[Commit]Get Tran Failure,tranID is not a DbTransaction!(" + tranID + ")");
                    }
                    //else
                    //    throw new ApplicationException("[Commit]HttpContext.Current.Items do not contain tranID(" + tranID + ")");
                }
            }
            catch (Exception ex)
            {
                Tool.Error("Commit Tran Self Error", "ex", ex);
            }
        }

        public void RollbackTran(string tranID)
        {
            try
            {
                //throw new ApplicationException("[Rollback]Get Tran Failure,HttpContext.Current is null");
                if (HttpContext.Current != null)
                {
                    if (HttpContext.Current.Items.Contains(tranID))
                    {
                        DbTransaction tran = HttpContext.Current.Items[tranID] as DbTransaction;
                        if (tran != null)
                        {
                            DbConnection conn = tran.Connection;
                            tran.Rollback();
                            if (conn != null)
                                conn.Close();
                            HttpContext.Current.Items.Remove(tranID);
                            HttpContext.Current.Items.Remove("DB_" + tranID);

                            Tool.Warn("Rollback Transaction", "tranID", tranID);
                        }
                        //else
                        //    throw new ApplicationException("[Rollback]Get Tran Failure,tranID is not a DbTransaction!(" + tranID + ")");
                    }
                    //else
                    //    throw new ApplicationException("[Rollback]HttpContext.Current.Items do not contain tranID(" + tranID + ")");
                }
            }
            catch (Exception ex)
            {
                Tool.Error("Rollback Tran Self Error", "ex", ex);
            }

        }

        #endregion

        #region parameter settings

        public void NoUseDefaultTran(Dictionary<string, object> args)
        {
            args["__NODEFAULTTRAN"] = 1;
        }

        public void SetDB(Dictionary<string, object> args, string db)
        {
            args["__DB"] = db.IndexOf(".") < 0 ? "Database_" + db : db;
        }

        public void SetSql(Dictionary<string, object> args, string sql)
        {
            args["__SQL"] = sql;
        }

        public void SetOutputParams(Dictionary<string, object> args)
        {
            args["__OutputParams"] = 1;
        }


        public void SetTimeOut(Dictionary<string, object> args, int timeoutSeconds)
        {
            args["__TIMEOUT"] = timeoutSeconds;
            //Gscm transfer a lot of writing a lot of log waste
            //Tool.Info("Set Command TimeOut(oracle invalid)", "seconds", timeoutSeconds);
        }

        public Dictionary<string, object> GetOutputParams(Dictionary<string, object> args)
        {
            if (args.ContainsKey("__OutputParams"))
                return args["__OutputParams"] as Dictionary<string, object>;
            return null;
        }

        public void NoLogResult(Dictionary<string, object> args)
        {
            args["__NoLogResult"] = 1;
        }

        public void AddParameterManual(Dictionary<string, object> args)
        {
            args["__ADD_PARAMETERS"] = 1;
        }

        public void NoCache(Dictionary<string, object> args)
        {
            args["__NoCache"] = 1;
        }

        public bool IsMatchCatch(Dictionary<string, object> args)
        {
            return args.ContainsKey("__MatchCatch");
        }

        #endregion


        #region Main Method
        public virtual DataSet Query(string cmdName, Dictionary<string, object> args)
        {
            return Query(null, cmdName, args);
        }

        public virtual int Execute(string cmdName, Dictionary<string, object> args)
        {
            return this.Execute(null, cmdName, args);
        }
        #endregion

        public virtual int Execute(string tranID, string cmdName, Dictionary<string, object> args)
        {
            return this.Execute<int>(null, cmdName, args);
        }

        public virtual T Execute<T>(string tranID, string cmdName, Dictionary<string, object> args)
        {
            if (args == null)
                args = new Dictionary<string, object>();// nullArgs;
            Database db;
            string dbName;
            DbCommand cmd = PrepareCommand(cmdName, args, out db, out dbName);
            DbTransaction tran = null;
            try
            {
                object ret;
                if (tranID == null && !args.ContainsKey("__NODEFAULTTRAN"))
                    tranID = HttpContext.Current.Items["__DEFAULTTRANID"] as string;

                tran = this.getTran(db, tranID, true);

                if (tran == null)
                {
                    if (typeof(T) == typeof(int))
                    {
                        ret = db.ExecuteNonQuery(cmd);
                    }
                    else if (typeof(T) == typeof(DataSet))
                    {
                        ret = db.ExecuteDataSet(cmd);
                    }
                    else if (typeof(T) == typeof(IDataReader))
                    {
                        ret = db.ExecuteReader(cmd);
                    }
                    else
                    {
                        ret = db.ExecuteScalar(cmd);
                    }
                }
                else
                {
                    if (typeof(T) == typeof(int))
                    {
                        ret = db.ExecuteNonQuery(cmd, tran);
                    }
                    else if (typeof(T) == typeof(DataSet))
                    {
                        ret = db.ExecuteDataSet(cmd, tran);
                    }
                    else if (typeof(T) == typeof(IDataReader))
                    {
                        ret = db.ExecuteReader(cmd, tran);
                    }
                    else
                    {
                        ret = db.ExecuteScalar(cmd, tran);
                    }
                }
                /*
                /// <summary>
                 /// Note Log principle:
                 /// means DBHelper.Execute
                 /// If the data generated by the user's business process, such as the trial, refund, approval and so on. Should be recorded
                 /// If the system Job or system behavior, and should not need to record this Log data (need to call this class NoLogResult method to stop the log process)
                 /// Instead of recording the job of the system's behavior, such as sending Email Job, only records the beginning and end of the process of sending data can Email
                  * The same principle applies if a batch sends a text message, executes a scrapped sample for review, and a batch write back note.
                  * If initiated by the user (such as PccMessenger take the initiative to collect information, you generally do not need to log, because the message will complete the record data transmission line of this process)
                
                /// </summary>
                */
                if (!args.ContainsKey("__NoLogResult")
                    || args["__NoLogResult"].ToString().Trim() != "1")
                    addOutputParameters(cmd, args);
                /*if (this.OnExecute != null)
                    this.OnExecute(ret, cmd, tran, tranID, cmdName, args);*/
                return (T)ret;
            }
            catch (Exception ex)
            {
                Tool.Error("[DBHelper.Execute]", "tranID", tranID, "tran", tran == null ? "null" : "OK", "DBName", dbName, "cmdName", cmdName, " cmd", cmd, "ex", ex.Message);
                throw;
                
                /*throw new ApplicationException("Execute fail(Message:" + ex.Message
                   + " cmdName:" + cmdName
                   + " tranID:" + tranID
                   + " sql:" + cmd.CommandText, ex);*/
               
            }
        }


        public virtual DataSet Query(string tranID, string cmdName, Dictionary<string, object> args)
        {
            DataSet ds = null;
            if (args == null)
                args = new Dictionary<string, object>();// nullArgs;

            Database db;
            string dbName;
            DbCommand cmd = PrepareCommand(cmdName, args, out db, out dbName);
            DbTransaction tran = null;
            try
            {
                if (tranID == null && !args.ContainsKey("__NODEFAULTTRAN"))
                    tranID = HttpContext.Current.Items["__DEFAULTTRANID"] as string;

                tran = this.getTran(db, tranID, false);

                if (tran == null)
                    ds = db.ExecuteDataSet(cmd);
                else
                    ds = db.ExecuteDataSet(cmd, tran);

                addOutputParameters(cmd, args);

            }
            catch (Exception ex)
            {
                Tool.Error("[DBHelper.Query]", "tranID", tranID, "tran", tran == null ? "null" : "OK", "DBName", dbName, "cmdName", cmdName, "cmd", cmd, "ex", ex.Message);
                throw;
            }

            return ds;

        }

        DbCommand PrepareCommand(string cmdName, Dictionary<string, object> args, out Database db, out string dbName)
        {
            try
            {
                SqlHelper sqlHelper = SqlHelper.Instance;
                Dictionary<string, string> dic = sqlHelper.getCmdCfgToDic(cmdName);
                if(dic==null)
                    throw new ApplicationException("get sql file error:" + cmdName);
                dbName = dic["DB"];
                string text = sqlHelper.CommandSql(dic["Text"], args);

                string type = "";
                if (dic.ContainsKey("Type"))
                    type = dic["Type"];

                try
                {
                    db = ObjectFactory.Instance.Get<Database>(dbName);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("get db error:" + dbName + ",ex:" + ex.ToString());
                }

                DbCommand ret = null;
                if (type != null && type.Equals("procedure"))
                    ret = PrepareProcCommand(db, text.Trim().Replace("\r", "").Replace("\n", ""), args);
                else
                    ret = PrepareSqlCommand(db, text, args);

                if (args.ContainsKey("__TIMEOUT"))
                    ret.CommandTimeout = (int)args["__TIMEOUT"];


                return ret;

            }
            catch (Exception ex)
            {
                throw new ApplicationException("PrepareCommand exception,cmdName:" + cmdName + ",ex:" + ex.Message.ToString(),ex);
                //throw;
            }
        }


        DbCommand PrepareProcCommand(Database db, string procname, Dictionary<string, object> args)
        {
            DbCommand cmd = db.GetStoredProcCommand(procname);
            using (DbConnection conn = db.CreateConnection())
            {
                try
                {
                    cmd.Connection = conn;
                    cmd.Connection.Open();
                    if (db.DBType.Equals(DatabaseType.Oracle))
                        OracleCommandBuilder.DeriveParameters((OracleCommand)cmd);
                    else
                        SqlCommandBuilder.DeriveParameters((SqlCommand)cmd);
                    cmd.Connection.Close();
                    addProcParameters(db, cmd, args);
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("PrepareProcCommand Fail(in Connection Open or DeriveParameters)(Message:" + ex.Message
                           + " procname:" + procname
                           + " db:" + db.DBType.ToString(), ex);
                }

            }
            return cmd;
        }



        DbCommand PrepareSqlCommand(Database db, string sql, Dictionary<string, object> args)
        {
            DbCommand cmd = db.GetSqlStringCommand("");
            SqlDyHelper dyHelper = new SqlDyHelper(db, cmd, args);
            MatchEvaluator me = new MatchEvaluator(dyHelper.capText);
            sql = Regex.Replace(sql, @"\*[0-9_a-zA-Z]*?\*", me);
            cmd.CommandText = sql;
            return cmd;
        }



        #region Parameter
        void addOutputParameters(DbCommand cmd, Dictionary<string, object> args)
        {
            if (args.ContainsKey("__OutputParams"))
            {
                Dictionary<string, object> outputParams = new Dictionary<string, object>();
                foreach (DbParameter p in cmd.Parameters)
                {
                    if (p.Direction == ParameterDirection.InputOutput
                        || p.Direction == ParameterDirection.Output
                        || p.Direction == ParameterDirection.ReturnValue)
                        outputParams.Add(p.ParameterName, p.Value);
                }
                args["__OutputParams"] = outputParams;
            }
        }


        void addProcParameters(Database db, DbCommand cmd, Dictionary<string, object> args)
        {
            if (args != null)
            {


                if (args.ContainsKey("__ADD_PARAMETERS") && args["__ADD_PARAMETERS"].ToString() == "1")
                {
                    foreach (string key in args.Keys)
                    {
                        if (!key.StartsWith("__"))
                            cmd.Parameters.Add(db.CreateParameter(key, args[key] ?? DBNull.Value));
                    }
                }
                else
                {
                    for (int i = cmd.Parameters.Count - 1; i >= 0; i--)
                    {
                        DbParameter p = cmd.Parameters[i];
                        if (p.Direction.Equals(ParameterDirection.Input) || p.Direction.Equals(ParameterDirection.InputOutput))
                        {
                            string argKey = p.ParameterName.Replace("@", "");   //sql server的參數會帶@
                            if (args.ContainsKey(argKey))
                            {
                                p.Value = args[argKey] ?? DBNull.Value;
                            }
                            else
                            {
                                //If the parameter is not passed, the default parameter of procedure is used
                                //At present, it should not affect the system, if any, and then revised                         
                                cmd.Parameters.RemoveAt(i);
                                //p.Value = DBNull.Value;
                            }
                        }
                    }
                }

            }
        }


        #endregion


        class SqlDyHelper
        {
            public SqlDyHelper(Database db, DbCommand cmd, Dictionary<string, object> args)
            {
                _db = db;
                _cmd = cmd;
                _args = args;
            }

            Dictionary<string, object> _args;
            DbCommand _cmd;
            Database _db;

            public string capText(Match match)
            {
                string mStr = match.ToString();
                string paramName = mStr.Substring(1, mStr.Length - 2);        //*paramName*
                if (!_args.ContainsKey(paramName))
                    throw new ApplicationException("The sql paramter is not provided(Param:" + paramName + ")");
                else
                {
                    string realParamName = (_db.DBType.Equals(DatabaseType.Oracle) ? "" : "@") + paramName;
                    if (!_cmd.Parameters.Contains(realParamName))
                    {
                        // Do not know, why, if this way plus Parameter, will let QueryReader, oracle long type of field inquiries, slow, so instead of the following way
                        // The reason is temporarily unknown, may be Database.ConfigureParameter method to move to what?
                        //_db.AddInParameter(_cmd, realParamName, _args[paramName]);
                        _cmd.Parameters.Add(_db.CreateParameter(realParamName, _args[paramName] ?? DBNull.Value));
                    }
                    return (_db.DBType.Equals(DatabaseType.Oracle) ? ":" : "@") + paramName;
                }
            }
        }





    }


}
