using System;
using System.Data;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;
using System.Data.Common;

namespace NService.Tools
{
    internal class JsonConverter
    {
        private static void WriteDataRow(StringBuilder sb, DataRow row)
        {
            sb.Append("{");
            foreach (DataColumn column in row.Table.Columns)
            {
                sb.AppendFormat("\"{0}\":", column.ColumnName);
                WriteValue(sb,row.RowState.Equals(DataRowState.Deleted)?row[column,DataRowVersion.Original]:row[column]);
                sb.Append(",");
            }

            // Remove the trailing comma.
            if (row.Table.Columns.Count > 0)
            {
                --sb.Length;
            }
            sb.Append("}");
        }

        private static void WriteDataSet(StringBuilder sb, DataSet ds)
        {
            sb.Append("{\"Tables\":{");
            foreach (DataTable table in ds.Tables)
            {
                sb.AppendFormat("\"{0}\":", table.TableName);
                WriteDataTable(sb, table);
                sb.Append(",");
            }
            // Remove the trailing comma.
            if (ds.Tables.Count > 0)
            {
                --sb.Length;
            }
            sb.Append("}}");
        }

        private static void WriteDataTable(StringBuilder sb, DataTable table)
        {
            sb.Append("{\"Rows\":[");
            foreach (DataRow row in table.Rows)
            {
                WriteDataRow(sb, row);
                sb.Append(",");
            }
            // Remove the trailing comma.
            if (table.Rows.Count > 0)
            {
                --sb.Length;
            }
            sb.Append("]}");
        }

        private static void WriteDataRows(StringBuilder sb, DataRowCollection rows)
        {
            sb.Append("[");
            foreach (DataRow row in rows)
            {
                WriteDataRow(sb, row);
                sb.Append(",");
            }
            // Remove the trailing comma.
            if (rows.Count > 0)
            {
                --sb.Length;
            }
            sb.Append("]");
        }

        private static void WriteEnumerable(StringBuilder sb, IEnumerable e)
        {
            bool hasItems = false;
            sb.Append("[");
            foreach (object val in e)
            {
                WriteValue(sb, val);
                sb.Append(",");
                hasItems = true;
            }
            // Remove the trailing comma.
            if (hasItems)
            {
                --sb.Length;
            }
            sb.Append("]");
        }

        private static void WriteHashtable(StringBuilder sb, Hashtable e)
        {
            bool hasItems = false;
            sb.Append("{");
            foreach (string key in e.Keys)
            {
                sb.AppendFormat("\"{0}\":", key.ToLower());
                WriteValue(sb, e[key]);
                sb.Append(",");
                hasItems = true;
            }
            // Remove the trailing comma.
            if (hasItems)
            {
                --sb.Length;
            }
            sb.Append("}");
        }

        private static void WriteReadOnlyDictionary(StringBuilder sb,ReadOnlyDictionary<string,object> e)
        {
            bool hasItems = false;
            sb.Append("{");
            foreach (string key in e.Keys)
            {
                sb.AppendFormat("\"{0}\":", key);
                WriteValue(sb, e[key]);
                sb.Append(",");
                hasItems = true;
            }
            // Remove the trailing comma.
            if (hasItems)
            {
                --sb.Length;
            }
            sb.Append("}");
        }

        private static void WriteDictionary(StringBuilder sb, IDictionary e)
        {
            bool hasItems = false;
            sb.Append("{");
            foreach (string key in e.Keys)
            {
                sb.AppendFormat("\"{0}\":", key);
                WriteValue(sb, e[key]);
                sb.Append(",");
                hasItems = true;
            }
            // Remove the trailing comma.
            if (hasItems)
            {
                --sb.Length;
            }
            sb.Append("}");
        }

        private static void WriteEntity(StringBuilder sb, object o)
        {
            sb.Append("{");
            bool hasMembers = false;
            Type type = o.GetType();
            List<string> addedFields = new List<string>();
            while (true)
            {
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo field in fields)
                {
                    if (!addedFields.Contains(field.Name))
                    {
                        addedFields.Add(field.Name);
                        sb.Append("\"");
                        sb.Append(field.Name);
                        sb.Append("\":");
                        WriteValue(sb, field.GetValue(o));
                        sb.Append(",");
                        hasMembers = true;
                    }
                }
                if (type.BaseType ==null || type.BaseType==typeof(object) || type == typeof(NService.DDD.Entity))
                    break;
                type = type.BaseType;
            }
            if (hasMembers)
            {
                --sb.Length;
            }
            sb.Append("}");
        }

        private static void WriteObject(StringBuilder sb, object o)
        {
            MemberInfo[] members = o.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public);
            sb.Append("{");
            bool hasMembers = false;
            foreach (MemberInfo member in members)
            {
                bool hasValue = false;
                object val = null;
                if ((member.MemberType & MemberTypes.Field) == MemberTypes.Field)
                {
                    FieldInfo field = (FieldInfo)member;
                    val = field.GetValue(o);
                    hasValue = true;
                }
                else if ((member.MemberType & MemberTypes.Property) == MemberTypes.Property)
                {
                    PropertyInfo property = (PropertyInfo)member;
                    if (property.CanRead && property.GetIndexParameters().Length == 0)
                    {
                        val = property.GetValue(o, null);
                        hasValue = true;
                    }
                }
                if (hasValue)
                {
                    sb.Append("\"");
                    sb.Append(member.Name);
                    sb.Append("\":");
                    WriteValue(sb, val);
                    sb.Append(",");
                    hasMembers = true;
                }
            }
            if (hasMembers)
            {
                --sb.Length;
            }
            sb.Append("}");
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        int i = (int)c;
                        if (i < 32 || i > 127)
                        {
                            sb.AppendFormat("\\u{0:X04}", i);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append("\"");
        }

        private static void WriteCommand(StringBuilder sb, DbCommand command)
        {
            Dictionary<string, object> paramsDic = null;
            if (command.Parameters != null && command.Parameters.Count > 0)
            {
                paramsDic = new Dictionary<string, object>();
                foreach (DbParameter p in command.Parameters)
                    paramsDic.Add(p.ParameterName, p.Value);
            }
            Dictionary<string, object> cmdDic = Tool.ToDic(
                "Type", command.CommandType
                , "Text", command.CommandText
                , "Parameters", paramsDic
            );
            WriteDictionary(sb, cmdDic);
        }

        
        public static void WriteValue(StringBuilder sb, object val)
        {
            if (val is Assembly)
            {
                sb.Append(((Assembly)val).FullName);
            }
            else if (val == null || val == System.DBNull.Value)
            {
                sb.Append("null");
            }
            else if (val is string || val is Guid)
            {
                WriteString(sb, val.ToString());
            }
            else if (val is bool)
            {
                sb.Append(val.ToString().ToLower());
            }
            else if (val is double ||
                val is float ||
                val is long ||
                val is int ||
                val is short ||
                val is byte ||
                val is decimal)
            {
                sb.AppendFormat(CultureInfo.InvariantCulture.NumberFormat, "{0}", val);
            }
            else if (val.GetType().IsEnum)
            {
                sb.Append((int)val);
            }
            else if (val is DateTime)
            {
                //sb.Append("new Date(\"");
                //sb.Append(((DateTime)val).ToString("MMMM, d yyyy HH:mm:ss", new CultureInfo("en-US", false).DateTimeFormat));
                //sb.Append("\")");
                sb.Append("\"");
                sb.Append(((DateTime)val).ToString("yyyy/MM/dd HH:mm:ss"));
                sb.Append("\"");
            }
            else if (val is DataSet)
            {
                WriteDataSet(sb, val as DataSet);
            }
            else if (val is DataTable)
            {
                WriteDataTable(sb, val as DataTable);
            }
            else if (val is DataRowCollection)
            {
                WriteDataRows(sb, val as DataRowCollection);
            }

            else if (val is DataRow)
            {
                WriteDataRow(sb, val as DataRow);
            }
            else if (val is Hashtable)
            {
                WriteHashtable(sb, val as Hashtable);
            }
            //else if (val is Dictionary<string, object>)
            //{
            //    WriteDictionary(sb, val as Dictionary<string, object>);
            //}

            else if (val is ReadOnlyDictionary<string, object>)
            {
                WriteReadOnlyDictionary(sb, val as ReadOnlyDictionary<string, object>);
            }
            else if (val is IDictionary)
            {
                WriteDictionary(sb, val as IDictionary);
            }

            else if (val is IEnumerable)
            {
                WriteEnumerable(sb, val as IEnumerable);
            }
            else if (val is System.Data.OracleClient.OracleLob)
            {
                WriteString(sb, ((System.Data.OracleClient.OracleLob)val).Value.ToString());
            }
            else if (val is DbCommand)
                WriteCommand(sb, val as DbCommand);
            else if (val is NService.DDD.Entity)
                WriteEntity(sb, val);
            else
            {
                //如果是Entity的內部類，只反射Field?私有Field呢?
                bool isEntityInternalType = val.GetType().IsNested && val.GetType().DeclaringType.IsSubclassOf(typeof(NService.DDD.Entity));
                if(isEntityInternalType)
                    WriteEntity(sb,val);
                else
                    WriteObject(sb, val);
            }
        }
        
        public static string Convert2Json(object o)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, o);
            return sb.ToString();
        }
    }
}