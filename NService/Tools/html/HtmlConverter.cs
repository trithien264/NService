using System;
using System.Data;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Globalization;

namespace NService.Tools
{
    internal class HtmlConverter
    {
        private static void WriteDataRow(StringBuilder sb, DataRow row)
        {
            sb.Append("<tr>");
            foreach (DataColumn column in row.Table.Columns)
            {
                sb.AppendFormat("<td class='{0}'>", column.ColumnName);
                WriteValue(sb, row[column]);
                sb.Append("</td>");
            }
            sb.Append("</tr>");
        }

        private static void WriteDataSet(StringBuilder sb, DataSet ds)
        {
            sb.Append("<ul>");
            foreach (DataTable table in ds.Tables)
            {
                sb.Append("<li>");
                WriteDataTable(sb, table);
                sb.Append("</li>");
            }
            sb.Append("</ul>");
        }

        private static void WriteDataTable(StringBuilder sb, DataTable table)
        {
            sb.Append("<table>");
            sb.Append("<thead>");
            sb.Append("<tr>");
            foreach (DataColumn column in table.Columns)
                sb.AppendFormat("<td class='{0}'>{0}</td>", column.ColumnName);
            sb.Append("</tr>");
            sb.Append("</thead>");
            sb.Append("<tbody>");
            foreach (DataRow row in table.Rows)
            {
                //sb.Append("<li>");
                WriteDataRow(sb, row);
                //sb.Append("</li>");
            }
            sb.Append("</tbody>");
            sb.Append("</table>");
        }

        private static void WriteDataRows(StringBuilder sb, DataRowCollection rows)
        {
            if (rows != null && rows.Count > 0)
            {
                sb.Append("<table>");
                sb.Append("<thead>");
                sb.Append("<tr>");
                foreach (DataColumn column in rows[0].Table.Columns)
                    sb.AppendFormat("<td class='{0}'>{0}</td>", column.ColumnName);
                sb.Append("</tr>");
                sb.Append("</thead>");
                sb.Append("<tbody>");
                foreach (DataRow row in rows)
                {
                    WriteDataRow(sb, row);
                }
                sb.Append("</tbody>");
                sb.Append("</table>");
            }
        }

        private static void WriteEnumerable(StringBuilder sb, IEnumerable e)
        {
            sb.Append("<ul>");
            foreach (object val in e)
            {
                sb.Append("<li>");
                WriteValue(sb, val);
                sb.Append("</li>");
            }
            sb.Append("</ul>");
        }

        private static void WriteHashtable(StringBuilder sb, Hashtable e)
        {
            sb.Append("<ul>");
            foreach (string key in e.Keys)
            {
                sb.AppendFormat("<li class='{0}'><h2>{0}</h2><div>", key.ToLower());
                WriteValue(sb, e[key]);
                sb.Append("</div></li>");
            }
            sb.Append("</ul>");
        }
        
        private static void WriteDictionary(StringBuilder sb, IDictionary e)
        {
            sb.Append("<ul>");
            foreach (string key in e.Keys)
            {
                sb.AppendFormat("<li class='{0}'><h2>{0}</h2><div>", key);
                WriteValue(sb, e[key]);
                sb.Append("</div></li>");
            }
            sb.Append("</ul>");
        }

        private static void WriteObject(StringBuilder sb, object o)
        {
            MemberInfo[] members = o.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public);
            sb.Append("<ul>");
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
                    sb.AppendFormat("<li class='{0}'><h2>{0}</h2><div>", member.Name);
                    WriteValue(sb, val);
                    sb.Append("</div></li>");
                }
            }
            sb.Append("</ul>");
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append(System.Web.HttpUtility.HtmlEncode(s));
        }
        
        public static void WriteValue(StringBuilder sb, object val)
        {
            if (val == null || val == System.DBNull.Value)
            {
                sb.Append("&nbsp;");
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
                sb.Append(((DateTime)val).ToString("yyyy/MM/dd HH:mm:ss"));
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
            else
            {
                WriteObject(sb, val);
            }
        }
        
        public static string Convert2Html(object o)
        {
            StringBuilder sb = new StringBuilder();
            WriteValue(sb, o);
            return sb.ToString();
        }
    }
}