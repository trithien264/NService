using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;


namespace NService.Tools
{

    internal class JavaScriptObjectDeserializer
    {
        private int _depthLimit;
        internal JavaScriptString _s;
        //private JavaScriptSerializer _serializer;
        private const string DateTimePrefix = "\"\\/Date(";
        private const int DateTimePrefixLength = 8;

        //private JavaScriptObjectDeserializer(string input, int depthLimit, JavaScriptSerializer serializer)
        private JavaScriptObjectDeserializer(string input, int depthLimit)
        {
            this._s = new JavaScriptString(input);
            this._depthLimit = depthLimit;
            //this._serializer = serializer;
        }

        private void AppendCharToBuilder(char? c, StringBuilder sb)
        {
            char? nullable = c;
            if (!((nullable.GetValueOrDefault() == '"') && nullable.HasValue))
            {
                char? nullable2 = c;
                if (!((nullable2.GetValueOrDefault() == '\'') && nullable2.HasValue))
                {
                    char? nullable3 = c;
                    if (!((nullable3.GetValueOrDefault() == '/') && nullable3.HasValue))
                    {
                        char? nullable4 = c;
                        if ((nullable4.GetValueOrDefault() == 'b') && nullable4.HasValue)
                        {
                            sb.Append('\b');
                        }
                        else
                        {
                            char? nullable5 = c;
                            if ((nullable5.GetValueOrDefault() == 'f') && nullable5.HasValue)
                            {
                                sb.Append('\f');
                            }
                            else
                            {
                                char? nullable6 = c;
                                if ((nullable6.GetValueOrDefault() == 'n') && nullable6.HasValue)
                                {
                                    sb.Append('\n');
                                }
                                else
                                {
                                    char? nullable7 = c;
                                    if ((nullable7.GetValueOrDefault() == 'r') && nullable7.HasValue)
                                    {
                                        sb.Append('\r');
                                    }
                                    else
                                    {
                                        char? nullable8 = c;
                                        if ((nullable8.GetValueOrDefault() == 't') && nullable8.HasValue)
                                        {
                                            sb.Append('\t');
                                        }
                                        else
                                        {
                                            char? nullable9 = c;
                                            if ((nullable9.GetValueOrDefault() != 'u') || !nullable9.HasValue)
                                            {
                                                string JSON_BadEscape = "Unrecognized escape sequence.";

                                                //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_BadEscape));
                                                throw new ArgumentException(this._s.GetDebugString(JSON_BadEscape));
                                            }
                                            sb.Append((char)int.Parse(this._s.MoveNext(4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                                        }
                                    }
                                }
                            }
                        }
                        return;
                    }
                }
            }
            sb.Append(c);
        }

        public static object DeserializeObject(string jsonStr)
        {
            return BasicDeserialize(jsonStr);
        }

        public static Dictionary<string, object> DeserializeDic(string jsonStr)
        {
            object o = BasicDeserialize(jsonStr);
            if (o != null && o is Dictionary<string, object>)
                return o as Dictionary<string, object>;
            return null;
        }

        public static ArrayList DeserializeArrayList(string jsonStr)
        {
            object o = BasicDeserialize(jsonStr);
            if (o != null && o is ArrayList)
                return o as ArrayList;
            return null;
        }

        public static List<Dictionary<string, object>> DeserializeList(string jsonStr)
        {
            object o = BasicDeserialize(jsonStr);
            if (o != null && o is ArrayList)
            {
                ArrayList list = o as ArrayList;
                List<Dictionary<string, object>> ret = new List<Dictionary<string, object>>();
                foreach (object item in list)
                    ret.Add( item as Dictionary<string, object>);
                return ret;
            }
            return null;
        }

        //internal static object BasicDeserialize(string input, int depthLimit, JavaScriptSerializer serializer)
        internal static object BasicDeserialize(string input)
        {
            int depthLimit = 100;       //JavaScriptSerializer.RecursionLimit = 100;
            JavaScriptObjectDeserializer deserializer = new JavaScriptObjectDeserializer(input, depthLimit);
            object obj2 = deserializer.DeserializeInternal(0);
            char? nextNonEmptyChar = deserializer._s.GetNextNonEmptyChar();
            int? nullable3 = nextNonEmptyChar.HasValue ? new int?(nextNonEmptyChar.GetValueOrDefault()) : null;
            if (nullable3.HasValue)
            {
                string JSON_IllegalPrimitive = "Invalid JSON primitive: {0}.";

                //throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, AtlasWeb.JSON_IllegalPrimitive, new object[] { deserializer._s.ToString() }));
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, JSON_IllegalPrimitive, new object[] { deserializer._s.ToString() }));
            }
            return obj2;
        }

        private char CheckQuoteChar(char? c)
        {
            char? nullable = c;
            if ((nullable.GetValueOrDefault() == '\'') && nullable.HasValue)
            {
                return c.Value;
            }
            char? nullable2 = c;
            if ((nullable2.GetValueOrDefault() != '"') || !nullable2.HasValue)
            {
                string JSON_StringNotQuoted = "Invalid string passed in, '\"' expected.";

                //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_StringNotQuoted));
                throw new ArgumentException(this._s.GetDebugString(JSON_StringNotQuoted));
            }
            return '"';
        }

        private IDictionary<string, object> DeserializeDictionary(int depth)
        {
            string JSON_InvalidMemberName = "Invalid object passed in, member name expected.";
            string JSON_InvalidObject = "Invalid object passed in, ':' or '}' expected.";
            IDictionary<string, object> dictionary = null;
            char? nextNonEmptyChar;
            char? nullable8;
            char? nullable11;
            char? nullable2 = this._s.MoveNext();
            if ((nullable2.GetValueOrDefault() != '{') || !nullable2.HasValue)
            {
                string JSON_ExpectedOpenBrace = "Invalid object passed in, '{' expected.";
                //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_ExpectedOpenBrace));
                throw new ArgumentException(this._s.GetDebugString(JSON_ExpectedOpenBrace));
            }
        Label_018D:
            nullable8 = nextNonEmptyChar = this._s.GetNextNonEmptyChar();
            int? nullable10 = nullable8.HasValue ? new int?(nullable8.GetValueOrDefault()) : null;
            if (nullable10.HasValue)
            {
                this._s.MovePrev();
                char? nullable3 = nextNonEmptyChar;
                if ((nullable3.GetValueOrDefault() == ':') && nullable3.HasValue)
                {
                    //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_InvalidMemberName));
                    throw new ArgumentException(this._s.GetDebugString(JSON_InvalidMemberName));
                }
                string text = null;
                char? nullable4 = nextNonEmptyChar;
                if ((nullable4.GetValueOrDefault() != '}') || !nullable4.HasValue)
                {
                    text = this.DeserializeMemberName();
                    if (string.IsNullOrEmpty(text))
                    {
                        //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_InvalidMemberName));
                        throw new ArgumentException(this._s.GetDebugString(JSON_InvalidMemberName));
                    }
                    char? nullable5 = this._s.GetNextNonEmptyChar();
                    if ((nullable5.GetValueOrDefault() != ':') || !nullable5.HasValue)
                    {

                        //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_InvalidObject));
                        throw new ArgumentException(this._s.GetDebugString(JSON_InvalidObject));
                    }
                }
                if (dictionary == null)
                {
                    dictionary = new Dictionary<string, object>();
                    if (string.IsNullOrEmpty(text))
                    {
                        nextNonEmptyChar = this._s.GetNextNonEmptyChar();
                        goto Label_01CB;
                    }
                }
                object obj2 = this.DeserializeInternal(depth);
                //dictionary.set_Item(text, obj2);
                dictionary[text] = obj2;
                nextNonEmptyChar = this._s.GetNextNonEmptyChar();
                char? nullable6 = nextNonEmptyChar;
                if ((nullable6.GetValueOrDefault() != '}') || !nullable6.HasValue)
                {
                    char? nullable7 = nextNonEmptyChar;
                    if ((nullable7.GetValueOrDefault() != ',') || !nullable7.HasValue)
                    {
                        //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_InvalidObject));
                        throw new ArgumentException(this._s.GetDebugString(JSON_InvalidObject));
                    }
                    goto Label_018D;
                }
            }
        Label_01CB:
            nullable11 = nextNonEmptyChar;
            if ((nullable11.GetValueOrDefault() != '}') || !nullable11.HasValue)
            {
                //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_InvalidObject));
                throw new ArgumentException(this._s.GetDebugString(JSON_InvalidObject));
            }
            return dictionary;
        }

        private object DeserializeInternal(int depth)
        {
            if (++depth > this._depthLimit)
            {
                string JSON_DepthLimitExceeded = "RecursionLimit exceeded.";
                //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_DepthLimitExceeded));
                throw new ArgumentException(this._s.GetDebugString(JSON_DepthLimitExceeded));
            }
            char? c = this._s.GetNextNonEmptyChar();
            char? nullable2 = c;
            int? nullable4 = nullable2.HasValue ? new int?(nullable2.GetValueOrDefault()) : null;
            if (!nullable4.HasValue)
            {
                return null;
            }
            this._s.MovePrev();
            if (this.IsNextElementDateTime())
            {
                return this.DeserializeStringIntoDateTime();
            }
            if (IsNextElementObject(c))
            {
                IDictionary<string, object> o = this.DeserializeDictionary(depth);
                if (o.ContainsKey("__type"))
                {
                    throw new Exception("can't implement this action");
                    //return ObjectConverter.ConvertObjectToType(o, null, this._serializer);
                }
                return o;
            }
            if (IsNextElementArray(c))
            {
                return this.DeserializeList(depth);
            }
            if (IsNextElementString(c))
            {
                return this.DeserializeString();
            }
            return this.DeserializePrimitiveObject();
        }

        private IList DeserializeList(int depth)
        {
            char? nextNonEmptyChar;
            char? nullable5;
            IList list = new ArrayList();
            char? nullable2 = this._s.MoveNext();
            if ((nullable2.GetValueOrDefault() != '[') || !nullable2.HasValue)
            {
                string JSON_InvalidArrayStart = "Invalid array passed in, '[' expected.";
                //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_InvalidArrayStart));
                throw new ArgumentException(this._s.GetDebugString(JSON_InvalidArrayStart));
            }
            bool flag = false;
        Label_00C4:
            nullable5 = nextNonEmptyChar = this._s.GetNextNonEmptyChar();
            int? nullable7 = nullable5.HasValue ? new int?(nullable5.GetValueOrDefault()) : null;
            if (nullable7.HasValue)
            {
                char? nullable8 = nextNonEmptyChar;
                if ((nullable8.GetValueOrDefault() != ']') || !nullable8.HasValue)
                {
                    this._s.MovePrev();
                    object obj2 = this.DeserializeInternal(depth);
                    list.Add(obj2);
                    flag = false;
                    nextNonEmptyChar = this._s.GetNextNonEmptyChar();
                    char? nullable3 = nextNonEmptyChar;
                    if ((nullable3.GetValueOrDefault() != ']') || !nullable3.HasValue)
                    {
                        flag = true;
                        char? nullable4 = nextNonEmptyChar;
                        if ((nullable4.GetValueOrDefault() != ',') || !nullable4.HasValue)
                        {
                            string JSON_InvalidArrayExpectComma = "Invalid array passed in, ',' expected.";
                            //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_InvalidArrayExpectComma));
                            throw new ArgumentException(this._s.GetDebugString(JSON_InvalidArrayExpectComma));
                        }
                        goto Label_00C4;
                    }
                }
            }
            if (flag)
            {
                string JSON_InvalidArrayExtraComma = "Invalid array passed in, extra trailing ','.";
                //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_InvalidArrayExtraComma));
                throw new ArgumentException(this._s.GetDebugString(JSON_InvalidArrayExtraComma));
            }
            char? nullable9 = nextNonEmptyChar;
            if ((nullable9.GetValueOrDefault() != ']') || !nullable9.HasValue)
            {
                string JSON_InvalidArrayEnd = "Invalid array passed in, ']' expected.";
                //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_InvalidArrayEnd));
                throw new ArgumentException(this._s.GetDebugString(JSON_InvalidArrayEnd));
            }
            return list;
        }

        private string DeserializeMemberName()
        {
            char? c = this._s.GetNextNonEmptyChar();
            char? nullable2 = c;
            int? nullable4 = nullable2.HasValue ? new int?(nullable2.GetValueOrDefault()) : null;
            if (!nullable4.HasValue)
            {
                return null;
            }
            this._s.MovePrev();
            if (IsNextElementString(c))
            {
                return this.DeserializeString();
            }
            return this.DeserializePrimitiveToken();
        }

        private object DeserializePrimitiveObject()
        {
            double result;
            string s = this.DeserializePrimitiveToken();
            if (s.Equals("null"))
            {
                return null;
            }
            if (s.Equals("true"))
            {
                return true;
            }
            if (s.Equals("false"))
            {
                return false;
            }
            bool flag = s.IndexOf('.') >= 0;
            //if (s.LastIndexOf("e", 5) < 0)
            //e是科學計數法
            //有e直接轉double
            //小數點先轉decimal，再轉double
            //整數先轉int,long,decimal,double
            //沒有float傳出
            if (s.LastIndexOf("e", StringComparison.OrdinalIgnoreCase) < 0)
            {
                decimal num3;
                if (!flag)
                {
                    int num;
                    long num2;
                    if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out num))
                    {
                        return num;
                    }
                    if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out num2))
                    {
                        return num2;
                    }
                }
                if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out num3))
                {
                    return num3;
                }
            }
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                string JSON_IllegalPrimitive = "Invalid JSON primitive: {0}.";
                //throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, AtlasWeb.JSON_IllegalPrimitive, new object[] { s }));
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, JSON_IllegalPrimitive, new object[] { s }));
            }
            return result;
        }

        private string DeserializePrimitiveToken()
        {
            char? nullable2;
            StringBuilder builder = new StringBuilder();
            char? nullable = null;
        Label_0066:
            nullable2 = nullable = this._s.MoveNext();
            int? nullable4 = nullable2.HasValue ? new int?(nullable2.GetValueOrDefault()) : null;
            if (nullable4.HasValue)
            {
                if ((char.IsLetterOrDigit(nullable.Value) || (nullable.Value == '.')) || (((nullable.Value == '-') || (nullable.Value == '_')) || (nullable.Value == '+')))
                {
                    builder.Append(nullable);
                }
                else
                {
                    this._s.MovePrev();
                    goto Label_00A2;
                }
                goto Label_0066;
            }
        Label_00A2:
            return builder.ToString();
        }

        private string DeserializeString()
        {
            StringBuilder sb = new StringBuilder();
            bool flag = false;
            char? c = this._s.MoveNext();
            char ch = this.CheckQuoteChar(c);
            while (true)
            {
                char? nullable4 = c = this._s.MoveNext();
                int? nullable6 = nullable4.HasValue ? new int?(nullable4.GetValueOrDefault()) : null;
                if (!nullable6.HasValue)
                {
                    string JSON_UnterminatedString = "Unterminated string passed in.";
                    //throw new ArgumentException(this._s.GetDebugString(AtlasWeb.JSON_UnterminatedString));
                    throw new ArgumentException(this._s.GetDebugString(JSON_UnterminatedString));
                }
                char? nullable2 = c;
                if ((nullable2.GetValueOrDefault() == '\\') && nullable2.HasValue)
                {
                    if (flag)
                    {
                        sb.Append('\\');
                        flag = false;
                    }
                    else
                    {
                        flag = true;
                    }
                }
                else if (flag)
                {
                    this.AppendCharToBuilder(c, sb);
                    flag = false;
                }
                else
                {
                    char? nullable3 = c;
                    int num = ch;
                    if ((nullable3.GetValueOrDefault() == num) && nullable3.HasValue)
                    {
                        return sb.ToString();
                    }
                    sb.Append(c);
                }
            }
        }

        private object DeserializeStringIntoDateTime()
        {
            long num;
            Match match = Regex.Match(this._s.ToString(), "^\"\\\\/Date\\((?<ticks>-?[0-9]+)(?:[a-zA-Z]|(?:\\+|-)[0-9]{4})?\\)\\\\/\"");
            if (long.TryParse(match.Groups["ticks"].Value, out num))
            {
                this._s.MoveNext(match.Length);

                DateTime time = new DateTime(0x7b2, 1, 1, 0, 0, 0, 1);
                long DatetimeMinTimeTicks = time.Ticks;

                //return new DateTime((num * 0x2710) + JavaScriptSerializer.DatetimeMinTimeTicks, 1);
                return new DateTime((num * 0x2710) + DatetimeMinTimeTicks, System.DateTimeKind.Utc); //.1);
            }
            return this.DeserializeString();
        }

        private static bool IsNextElementArray(char? c)
        {
            char? nullable = c;
            if (nullable.GetValueOrDefault() == '[')
            {
                return nullable.HasValue;
            }
            return false;
        }

        private bool IsNextElementDateTime()
        {
            string text = this._s.MoveNext(8);
            if (text != null)
            {
                this._s.MovePrev(8);
                return string.Equals(text, "\"\\/Date(", System.StringComparison.Ordinal);// 4);
            }
            return false;
        }

        private static bool IsNextElementObject(char? c)
        {
            char? nullable = c;
            if (nullable.GetValueOrDefault() == '{')
            {
                return nullable.HasValue;
            }
            return false;
        }

        private static bool IsNextElementString(char? c)
        {
            char? nullable = c;
            if ((nullable.GetValueOrDefault() == '"') && nullable.HasValue)
            {
                return true;
            }
            char? nullable2 = c;
            if (nullable2.GetValueOrDefault() == '\'')
            {
                return nullable2.HasValue;
            }
            return false;
        }
    }
}
