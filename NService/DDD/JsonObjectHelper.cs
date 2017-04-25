using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.Serialization;
using System.Web;
using NService;
using NService.Tools;

namespace NService.DDD
{
    public class JsonObjectHelper
    {

        public T DicToObject<T>(Dictionary<string, object> dic) where T : class
        {
            T t = (T)FormatterServices.GetUninitializedObject(typeof(T));
            fillObjectByDic(t, dic);
            return t;
        }

        public object DicToObject(Type type, Dictionary<string, object> dic)
        {
            object t = FormatterServices.GetUninitializedObject(type);
            fillObjectByDic(t, dic);
            return t;
        }

        //json的object轉為desType
        object jsonToType(object jsonObject, Type desType)
        {
            if (jsonObject != null && jsonObject.GetType() != desType)
            {
                if (jsonObject is ArrayList)
                {
                    ArrayList jsonList = (ArrayList)jsonObject;
                    IList fieldList = (IList)Activator.CreateInstance(desType);
                    Type[] fieldGenTypes = desType.GetGenericArguments();
                    foreach (object jsonItem in jsonList)
                    {
                        if (fieldGenTypes.Length > 0)
                            fieldList.Add(jsonToType(jsonItem, fieldGenTypes[0]));
                        else
                            fieldList.Add(jsonItem);        //如果不是一個范型的List，則回不來有可能
                    }
                    return fieldList;
                }
                else if (jsonObject is Dictionary<string, object>)
                {
                    Dictionary<string, object> jsonDic = (Dictionary<string, object>)jsonObject;
                    object fieldObj = Activator.CreateInstance(desType);
                    if (fieldObj is IDictionary)
                    {
                        Type[] fieldGenTypes = desType.GetGenericArguments();
                        IDictionary fieldDic = (IDictionary)fieldObj;
                        foreach (string key in jsonDic.Keys)
                            fieldDic.Add(jsonToType(key, fieldGenTypes[0]), jsonToType(jsonDic[key], fieldGenTypes[1]));
                        return fieldDic;
                    }
                    else
                    {
                        fillObjectByDic(fieldObj, jsonDic);
                        return fieldObj;
                    }
                }
                else
                    return Convert.ChangeType(jsonObject, desType);
            }
            return jsonObject;
        }

        void fillObjectByDic(object obj, Dictionary<string, object> dic)
        {
            Type type = obj.GetType();
            List<string> setFields = new List<string>();
            while (true)
            {
                //Entity的字段在外面hardcode反射，目前只有id（dic也不會包含Entity的屬性）(錯了，json會把id也寫入進去的)
                //if ()
                //    break;
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo field in fields)
                {
                    string fName = field.Name;// field["Name"].ToString();
                    if (!setFields.Contains(fName))     //每次都會帶出public和protected成員,所以父類也會再一遍
                    {
                        setFields.Add(fName);
                        if (dic.ContainsKey(fName))
                        {
                            object jsonValue = dic[fName];
                            field.SetValue(obj, jsonToType(jsonValue, field.FieldType));
                        }
                    }
                }
                //如果是Entity內部的嵌套類型，可能會到object，不過應該也可以對這個方法多加一個參數來設定（但現在沒必要）
                //Entity的東西還是在這里統一設定，因為JsonConverter序列化也進去了
                if (type.BaseType == null || type.BaseType == typeof(object) || type == typeof(NService.DDD.Entity))
                    break;
                type = type.BaseType;

            }
        }

    }
}