using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace NService.Tools
{
    internal class SqlFunction
    {

        public static readonly SqlFunction _instance = new SqlFunction();

        public static SqlFunction Instance
        {
            get
            {
                return _instance;
            }
        }

        /// <summary>
        /// 本類替換一般都以參數直接進行即 *ABC* 會生成一個參數ABC(除IsEqual,IsNotEqual和IsEmpty外，它們可以直接寫語句)
        /// </summary>
        /// <param name="cmdName"></param>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string Call(string cmdName, string paramsStr, Dictionary<string, object> args)
        {
            string ret = "";
            MethodInfo met = this.GetType().GetMethod(cmdName);
            if (met != null)
                ret = met.Invoke(this, new object[] { paramsStr, args }).ToString();
            else
            {
                ret = "@" + cmdName + "(" + paramsStr + ")";
            }
            //    throw new ApplicationException("Sql Function is Not found(" + cmdName + ")");
            return ret;
        }

        #region 內部調用

        bool isEmpty(string key, Dictionary<string, object> args)
        {
            return args == null
                || !args.ContainsKey(key)
                || args[key] == null
                || args[key].ToString().Trim().Length == 0
                || args[key].ToString().Trim().ToLower().Equals("null");
        }

        string andEmpty(string paramsStr, Dictionary<string, object> args, string exp)
        {
            string argName;
            return andEmpty(false,paramsStr, args, exp, out argName);
        }
        string andEmpty(string paramsStr, Dictionary<string, object> args, string exp, out string argName)
        {
            return andEmpty(true,paramsStr,args, exp, out argName);
        }

        string andEmpty(bool addNew,string paramsStr, Dictionary<string, object> args, string exp, out string argName)
        {
            string colName = paramsStr;
            argName = paramsStr;
            if (paramsStr.IndexOf(",") > 0)
            {
                colName = paramsStr.Split(new char[] { ',' })[0];
                argName = paramsStr.Split(new char[] { ',' })[1];
            }
            if (argName.IndexOf(".") > 0)
                argName = argName.Split(new char[] { '.' })[1];
            //這邊的colName不怕注入，是因為SQL是開發人員寫的(在服務器端)
            if (!isEmpty(argName, args))
                return string.Format(exp, colName, addNew?argName + "__NEW":argName);
            else
                argName = null;
            return "";
        }

        #endregion

        #region Sql Functions(internal)

        /// <summary>
        /// @IsEqual(BUSKIND,Lean,and gd.build_no != '9B')
        /// 如果args["BUSKIND"]=="Lean",則 and gd.build_no != '9B'
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string IsEqual(string paramsStr, Dictionary<string, object> args)
        {
            string[] tmp = paramsStr.Split(new char[] { ',' }, 3);    //不能用4個參數else，因為第三個可能會很復雜，里面有,號
            string argName = tmp[0];
            string equalValue = tmp[1];
            string statement = tmp[2];
            if (args != null && args[argName].ToString() == equalValue)
                return statement.Replace("{", "(").Replace("}", ")");
            return "";
        }

        /// <summary>
        /// @IsEqual(BUSKIND,Lean,and gd.build_no != '9B')
        /// 如果args["BUSKIND"]!="Lean",則 and gd.build_no != '9B'
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string IsNotEqual(string paramsStr, Dictionary<string, object> args)
        {
            string[] tmp = paramsStr.Split(new char[] { ',' }, 3);
            string argName = tmp[0];
            string equalValue = tmp[1];
            string statement = tmp[2];
            if (args != null && args[argName].ToString() != equalValue)
                return statement.Replace("{", "(").Replace("}", ")");
            return "";
        }

        /// <summary>
        /// @IsEmpty(TEST_DATE_FROM,AND M.TEST_DATE >= *TEST_DATE_FROM*)
        /// 如果args["TEST_DATE_FROM"]有值，則AND M.TEST_DATE >= args["TEST_DATE_FROM"]
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string IsEmpty(string paramsStr, Dictionary<string, object> args)
        {
            string[] tmp = paramsStr.Split(new char[] { ',' }, 2);
            string argName = tmp[0];
            string statement = tmp[1];
            if (!isEmpty(argName, args))
                return statement.Replace("{", "(").Replace("}", ")");
            return "";
        }

        /// <summary>
        /// @AndEmptyEqual(a.prod_factory)
        /// 如果args["prod_factory"]有值，則and a.prod_factory=args["prod_factory"]
        /// </summary>
        /// <param name="sqlCode"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string AndEmptyEqual(string paramsStr, Dictionary<string, object> args)
        {
            return andEmpty(paramsStr, args, " AND {0} = *{1}*");
        }

        /// <summary>
        /// @AndEmptyLessEqual(a.last_cfg_date)
        /// 如果args["last_cfg_date"]有值，則and a.last_cfg_date<=args["last_cfg_date"]
        /// </summary>
        /// <param name="sqlCode"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string AndEmptyLessEqual(string paramsStr, Dictionary<string, object> args)
        {
            return andEmpty(paramsStr, args, " AND {0} <= *{1}*");
        }

        /// <summary>
        /// @AndEmptyLess(a.last_cfg_date)
        /// 如果args["last_cfg_date"]有值，則and a.last_cfg_date<args["last_cfg_date"]
        /// </summary>
        /// <param name="sqlCode"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string AndEmptyLess(string paramsStr, Dictionary<string, object> args)
        {
            return andEmpty(paramsStr, args, " AND {0} < *{1}*");
        }

        /// <summary>
        /// @AndEmptyGreateEqual(a.last_cfg_date)
        /// 如果args["last_cfg_date"]有值，則and a.last_cfg_date>=args["last_cfg_date"]
        /// </summary>
        /// <param name="sqlCode"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string AndEmptyGreateEqual(string paramsStr, Dictionary<string, object> args)
        {
            return andEmpty(paramsStr, args, " AND {0} >= *{1}*");
        }

        /// <summary>
        /// @AndEmptyGreate(a.last_cfg_date)
        /// 如果args["last_cfg_date"]有值，則and a.last_cfg_date>args["last_cfg_date"]
        /// </summary>
        /// <param name="sqlCode"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string AndEmptyGreate(string paramsStr, Dictionary<string, object> args)
        {
            return andEmpty(paramsStr, args, " AND {0} > *{1}*");
        }

        /// <summary>
        /// @AndEmptyNotEqual(a.last_cfg_date)
        /// 如果args["last_cfg_date"]有值，則and a.last_cfg_date<>args["last_cfg_date"]
        /// </summary>
        /// <param name="sqlCode"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string AndEmptyNotEqual(string paramsStr, Dictionary<string, object> args)
        {
            return andEmpty(paramsStr, args, " AND {0} <> *{1}*");
        }

        /// <summary>
        /// @AndEmptyLike(REASON_IND)
        /// 如果args["REASON_IND"]有值，則and REASON_IND like %args["REASON_IND"]%
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string AndEmptyLike(string paramsStr, Dictionary<string, object> args)
        {
            string argName;
            string ret = andEmpty(paramsStr, args, " AND {0} LIKE *{1}*", out argName);
            if (argName != null)        //不影響傳入參數
                args[argName + "__NEW"] = "%" + args[argName].ToString() + "%";
            return ret;
        }

        /// <summary>
        /// @AndEmptyStart(REASON_IND)
        /// 如果args["REASON_IND"]有值，則and REASON_IND like args["REASON_IND"]%
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string AndEmptyStart(string paramsStr, Dictionary<string, object> args)
        {
            string argName;
            string ret = andEmpty(paramsStr, args, " AND {0} LIKE *{1}*", out argName);
            if (argName != null)
                args[argName+ "__NEW"] = args[argName].ToString() + "%";
            return ret;
        }

        /// <summary>
        /// @AndEmptyEnd(REASON_IND)
        /// 如果args["REASON_IND"]有值，則and REASON_IND like %args["REASON_IND"]
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string AndEmptyEnd(string paramsStr, Dictionary<string, object> args)
        {
            string argName;
            string ret = andEmpty(paramsStr, args, " AND {0} LIKE *{1}*", out argName);
            if (argName != null)
                args[argName+ "__NEW"] = "%" + args[argName].ToString();
            return ret;
        }

        public string AndEmptyIn(string paramsStr, Dictionary<string, object> args)
        {
            string argName;
            string ret = andEmpty(paramsStr, args, " AND {0} IN (####)", out argName);
            if (argName != null)
            {
                ret = ret.Replace("####",In(argName,args));
            }
            return ret;
        }

        #endregion

        #region Sql Functions(I/U/D/S)

        /// <summary>
        /// 防sql注入 在Insert,Update,Delete,Select的Field時進行檢驗，Value不用，因為一般都用參數傳值
        /// 還有In子句的值連結可能會被Sql注入
        /// </summary>
        /// <param name="field"></param>
        void checkField(string field)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(field, "[^a-zA-Z0-9_]"))
                throw new ApplicationException("this field is invalid:" + field);
        }


        public string In(string paramsStr, Dictionary<string, object> args)
        {
            string argName = paramsStr.Trim();
            string argValue = "";
            if (args.ContainsKey(argName) && args[argName] != null)
            {
                ArrayList items = args[argName] as ArrayList;
                if (items != null && items.Count > 0)
                {
                    foreach (string item in items)
                        argValue += (argValue.Length > 0 ? "," : "") + "'" + item.Replace("'","''") + "'";
                }
            }
            //string ret = "#" + argName + "__NEW#";
            //args[ret.Replace("#","")] = argValue;
            return argValue;
        }

        //防注入的in，用參數傳遞。暫時不寫了，因為argName的取名問題，要不要lock，也可以再檢查一下上面這個In函數
        //簡單的替換'是否有sql注入風險
        /*
        public string In2(string paramsStr, Dictionary<string, object> args)
        {
            string argName = paramsStr.Trim();
            string argValue = "";
            if (args.ContainsKey(argName) && args[argName] != null)
            {
                ArrayList items = args[argName] as ArrayList;
                if (items != null && items.Count > 0)
                {
                    foreach (string item in items)
                    {
                        string argName = "Auto_"
                        argValue += (argValue.Length > 0 ? "," : "") + "'" + item.Replace("'", "''") + "'";
                        args[argName + "__NEW"] = "%" + args[argName].ToString() + "%";
                    }
                }
            }
            //string ret = "#" + argName + "__NEW#";
            //args[ret.Replace("#","")] = argValue;
            return argValue;
        }
        */

        /// <summary>
        /// @Insert(WEB_BD_REASON|ITEMNO,REASON,REASON_INDO)
        /// Insert Table WEB_BD_REASON這三個欄位ITEMNO,REASON,REASON_INDO
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string Insert(string paramsStr, Dictionary<string, object> args)
        {
            string[] tmp = paramsStr.Split(new char[] { '|' });
            string tablename = tmp[0];
            StringBuilder colSb = new StringBuilder();
            if (tmp.Length == 1)
            {
                if (args != null && args.Count > 0)
                {
                    foreach (string key in args.Keys)
                    {
                        if (!key.EndsWith("!N") && !key.StartsWith("__"))
                        {
                            checkField(key);
                            colSb.AppendFormat("{0}{1}", colSb.Length > 0 ? "," : "", key);
                        }
                    }
                }
                else
                    throw new ApplicationException("No params set in insert,but actual params is null too(" + paramsStr + ")");
            }
            else if (tmp.Length == 2)
            {
                string[] paramList = tmp[1].Split(new char[] { ',' });
                foreach (string aParam in paramList)
                    colSb.AppendFormat("{0}{1}", colSb.Length > 0 ? "," : "", aParam);
            }
            else
                throw new ApplicationException("Insert Format:tablename|col1,col2(" + paramsStr + ")");
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("INSERT INTO {0}", tablename);
            sb.Append("(");
            sb.Append(colSb);
            sb.Append(")");
            sb.Append("VALUES");
            sb.Append("(*");
            sb.Append(colSb.Replace(",", "*,*"));
            sb.Append("*)");
            return sb.ToString();
        }

        /// <summary>
        /// @Update(WEB_BD_REASON|REASON,REASON_INDO|ITEMNO)
        /// Update Table WEB_BD_REASON這2個欄位REASON,REASON_INDO,且以ITEMNO=args["ITEMNO"]為條件
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string Update(string paramsStr, Dictionary<string, object> args)
        {
            string[] tmp = paramsStr.Split(new char[] { '|' });
            string tablename = tmp[0];
            StringBuilder colSb = new StringBuilder();
            StringBuilder whereSb = new StringBuilder();
            if (tmp.Length == 1)
            {
                if (args != null && args.Count > 0)
                {
                    foreach (string key in args.Keys)
                    {
                        if (!key.EndsWith("!N") && !key.StartsWith("__"))
                        {
                            if (!key.EndsWith("__W"))
                            {
                                checkField(key);
                                colSb.AppendFormat("{0}{1}=*{1}*", colSb.Length > 0 ? "," : "", key);
                            }
                            else
                            {
                                string newKey = key.Substring(0, key.Length - 3);
                                checkField(newKey);
                                whereSb.AppendFormat(" {0} {1}=*{2}*", whereSb.Length > 0 ? " AND " : "", newKey, key);
                            }

                        }
                    }
                }
                else
                    throw new ApplicationException("No params set in update,but actual params is null too(" + paramsStr + ")");
            }
            else if (tmp.Length == 3)
            {
                string[] paramList = tmp[1].Split(new char[] { ',' });
                foreach (string aParam in paramList)
                    colSb.AppendFormat("{0}{1}=*{1}*", colSb.Length > 0 ? "," : "", aParam);
                string[] whereParamList = tmp[2].Split(new char[] { ',' });
                foreach (string aParam in whereParamList)
                    whereSb.AppendFormat(" {0} {1}=*{1}*", whereSb.Length > 0 ? " AND " : "", aParam);
            }
            else
                throw new ApplicationException("Update Format:tablename|col1,col2|whereCol1,whereCol2(" + paramsStr + ")");

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("UPDATE {0}", tablename);
            sb.Append(" SET ");
            sb.Append(colSb);
            sb.Append(" WHERE ");
            sb.Append(whereSb);
            return sb.ToString();
        }

        /// <summary>
        /// @Delete(WEB_BD_REASON|ITEMNO)
        /// Update Table WEB_BD_REASON以ITEMNO=args["ITEMNO"]為條件
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string Delete(string paramsStr, Dictionary<string, object> args)
        {
            string[] tmp = paramsStr.Split(new char[] { '|' });
            string tablename = tmp[0];
            StringBuilder colSb = new StringBuilder();
            if (tmp.Length == 1)
            {
                if (args != null && args.Count > 0)
                {
                    foreach (string key in args.Keys)
                    {
                        if (!key.EndsWith("!N") && !key.StartsWith("__"))
                        {
                            checkField(key);
                            colSb.AppendFormat("{0}{1}=*{1}*", colSb.Length > 0 ? " AND " : "", key);
                        }
                    }
                }
                else
                    throw new ApplicationException("if you don't set params,you must give cols runtime (paramsStr:" + paramsStr + " )");
            }
            else if (tmp.Length == 2)
            {
                string[] paramList = tmp[1].Split(new char[] { ',' });
                foreach (string aParam in paramList)
                    colSb.AppendFormat("{0}{1}=*{1}*", colSb.Length > 0 ? " AND " : "", aParam);
            }
            else
                throw new ApplicationException("Delete Format:tablename|whereCol1,whereCol2(" + paramsStr + ")");

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("DELETE FROM {0}", tablename);
            sb.Append(" WHERE ");
            sb.Append(colSb);
            return sb.ToString();
        }

        /// <summary>
        /// @Select(WEB_BD_REASON)
        /// </summary>
        /// <param name="paramsStr"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string Select(string paramsStr, Dictionary<string, object> args)
        {
            string[] tmp = paramsStr.Split(new char[] { '-' });
            string tablename = tmp[0];
            StringBuilder colSb = new StringBuilder();
            if (tmp.Length == 1)
            {
                if (args != null && args.Count > 0)
                {
                    Dictionary<string, string> addArgs = new Dictionary<string, string>();
                    foreach (string key in args.Keys)
                    {
                        if (key != "@CUSTOMSQL" && !key.EndsWith("__NOAUTO") && key != "__NoCache" && !key.EndsWith("!N") && !key.StartsWith("__") && args[key] != null && args[key].ToString().Trim().Length > 0)
                        {
                            string flag = "=";
                            string field = key;
                            string param = args[key].ToString().Trim();
                            string newParam = param;
                            string newKey = key;
                            int index = key.IndexOf("__");
                            if (index > 0)
                            {
                                // G,GE,L,LE,NE,LIKE,START,END
                                flag = key.Substring(index + 2).ToUpper().Trim();
                                field = key.Substring(0, key.Length - flag.Length - 2);    // 欄位名:如:User_ID,User_Desc
                                newParam = param;
                                
                                #region 運算符解釋

                                if (flag == "LIKE")
                                {
                                    newParam = "%" + param + "%";
                                }
                                else if (flag == "START")
                                {
                                    flag = "LIKE";
                                    newParam = param + "%";
                                }
                                else if (flag == "END")
                                {
                                    flag = "LIKE";
                                    newParam = "%" + param;
                                }
                                else if (flag == "G")
                                {
                                    flag = ">";
                                }
                                else if (flag == "GE")
                                {
                                    flag = ">=";
                                }
                                else if (flag == "L")
                                {
                                    flag = "<";
                                }
                                else if (flag == "LE")
                                {
                                    flag = "<=";
                                }
                                else if (flag == "NE")
                                {
                                    flag = "<>";
                                }
                                else if (flag == "IN")
                                {
/*
 * 更安全的In寫法
------oracle--------
SELECT * FROM web_bond_test
WHERE sec_no 
IN (
SELECT '55A1' FROM dual
UNION
SELECT '55A2' from dual
)
AND test_date >= 20100501
 *
------sql server------
select * from wf_step where action in 
(
select 'pass' 
union select 'back'
)  
*/
                                    //先暫時這么辦，不知道會不會有注入風險?
                                    newParam = "('" + param.Replace("--","").Replace(",","','") + "')";

                                }
                                else
                                {
                                    throw new ApplicationException("Not support this expression:" + flag);
                                }

                                #endregion

                                if (newParam != param)
                                {
                                    newKey = key + "__NEW";
                                    addArgs[newKey] = newParam;
                                }

                            }
                            checkField(field);
                            colSb.AppendFormat("{0}{1} {2} {3}"
                                , colSb.Length > 0 ? " AND " : ""
                                , field
                                , flag
                                , flag=="IN"?newParam:"*" + newKey + "*");
                        }

                    }
                    foreach (string key in addArgs.Keys)
                        args.Add(key, addArgs[key]);
                }
                //else
                //    throw new ApplicationException("invalid Select Format,no params");
            }
            else if(tmp.Length>2)
                throw new ApplicationException("Select Format:tablename(" + paramsStr + ")");

            StringBuilder sb = new StringBuilder();
            if (tablename.IndexOf(",") < 0)
                //,不能成為路徑的一部份
                sb.AppendFormat("SELECT {1} FROM {0}", tablename,tmp.Length>1?tmp[1].Replace('.',','):"*");
            else
                sb.AppendFormat("SELECT {1} FROM ({0}) tbl", args["@CUSTOMSQL"].ToString(), tmp.Length > 1 ? tmp[1] : "*");
            sb.Append(colSb.Length>0?" WHERE ":"");
            sb.Append(colSb);
            return sb.ToString();
        }


        public string Custom(string paramStr, Dictionary<string, object> args)
        {
            return args["CUSTOMSQL"].ToString();
        }
        #endregion
    }
}