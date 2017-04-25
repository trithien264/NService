using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Reflection;
using System.IO;
using System.Web.Services.Description;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Net;

namespace NService.Tools
{
    public class ObjectFactory
    {
        public static readonly ObjectFactory Instance = new ObjectFactory(new FileConfig("ObjectFactory", new JsonParser()));


        public FileConfig _config;
        Dictionary<string, List<string>> _dyDllObjects;
        FileConfig _dyDllConfig;
        public ObjectFactory(FileConfig config)
        {
            _config = config;

            _dyDllObjects = new Dictionary<string, List<string>>();
            _dyDllConfig = new FileConfig("_service\\Dll", "", new AssemblyParser());
        }


        public IConfig Config
        {
            get
            {
                return _config;
            }
        }

        public FileConfig DyConfig
        {
            get
            {
                return _dyDllConfig;
            }
        }

        public object Get(string name)
        {
            return this.Get<object>(name);
        }

        object[] dealParams(ArrayList targs)
        {
            if (targs == null || targs.Count == 0)
                return null;
            object[] ret = new object[targs.Count];
            int i = 0;
            foreach (object arg in targs)
            {
                ret[i++] = arg;
            }
            return ret;
        }

        public T Get<T>() where T : class
        {
            return this.Get<T>(typeof(T).FullName);
        }

        public T Get<T>(string objectID) where T : class
        {
            string cacheKey = objectID;
            string configName = AppEventHanlder.Instance.ServiceVarContent();// ("Config");
            if (configName != null && configName.Length > 0)
            {
                cacheKey = objectID + "$" + configName;
            }

            lock (getLockObj(cacheKey))
            {

                string objectType = objectID;
                T ret = null;
                string dll = null;
                object[] args = null;
                string url = null;
                Dictionary<string, object> soapHeader = null;
                string dyDll = null;

                Dictionary<string, object> fields = null;

                object[] cfg = objectCfg(objectID);
                if (cfg != null)
                {
                    if (cfg[0] != null && cfg[0].ToString().Length > 0)
                        objectType = cfg[0].ToString();
                    dll = cfg[1] as string;
                    ArrayList targs = cfg[2] as ArrayList;
                    args = this.dealParams(targs);
                    url = cfg[3] as string;
                    soapHeader = cfg[4] as Dictionary<string, object>;
                    dyDll = cfg[5] as string;

                    fields = cfg[6] as Dictionary<string, object>;

                }


                if (url != null && url != "")
                    ret = CreateWSObject(objectType, url, soapHeader) as T;
                else if (fields != null)
                    ret = JsonToObject<T>(objectType, dll, fields, ref dyDll, objectType == objectID);
                else
                    ret = CreateObject<T>(objectType, dll, args, ref dyDll, objectType == objectID);

                if (ret != null)
                    ret = Register(cacheKey, ret, dyDll) as T;


                return ret;
            }
        }

        public T CreateObject<T>(string objectType, string dll, object[] args, ref string dyDll, bool tryT) where T : class
        {
            T ret = null;
            Type t = GetType<T>(objectType, dll, ref dyDll, tryT);
            if (t != null)
            {
                FieldInfo fi = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (fi != null)
                {
                    try
                    {
                        ret = fi.GetValue(null) as T;
                    }
                    catch (Exception ex)
                    {
                        if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                            Tool.Warn("Instance property fetch failed: ", "t.FullName", t.FullName, "ex", ex);
                    }
                }

                if (ret == null)
                {
                    try
                    {
                        ret = (T)Activator.CreateInstance(t, args);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception("Exception 'ObjectFactory.CreateObject': " + ex.InnerException.Message.ToString(), ex.InnerException == null ? ex : ex.InnerException);
                    }
                }
            }

            return ret;
        }

        public T JsonToObject<T>(string objectType, string dll, Dictionary<string, object> fields, ref string dyDll, bool tryT) where T : class
        {

            T ret = null;
            Type t = GetType<T>(objectType, dll, ref dyDll, tryT);
            //Instance for the static actual object, and constructor is private, direct access to Instance, not new
            if (t != null)
            {
                NService.DDD.JsonObjectHelper jHelper = new DDD.JsonObjectHelper();
                try
                {
                    ret = jHelper.DicToObject(t, fields) as T;
                }
                catch (Exception ex)
                {
                    if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                        Tool.Warn("Instance property get failed", "t.FullName", t.FullName, "ex", ex);
                }
            }
            return ret;
        }
        public object CreateWSObject(string objectType, string url, Dictionary<string, object> soapHeader)
        {
            object ret = null;
            string ns = objectType.Substring(0, objectType.LastIndexOf("."));
            string classname = objectType.Substring(objectType.LastIndexOf(".") + 1);
            ServiceDescriptionImporter importer1 = new ServiceDescriptionImporter();
            importer1.AddServiceDescription(ServiceDescription.Read(new WebClient().OpenRead(url + "?WSDL")), "", "");
            CodeNamespace namespace1 = new CodeNamespace(ns);
            CodeCompileUnit unit1 = new CodeCompileUnit();
            unit1.Namespaces.Add(namespace1);
            importer1.Import(namespace1, unit1);
            CompilerParameters parameters1 = new CompilerParameters();
            parameters1.GenerateExecutable = false;
            parameters1.GenerateInMemory = true;
            parameters1.ReferencedAssemblies.Add("System.dll");
            parameters1.ReferencedAssemblies.Add("System.XML.dll");
            parameters1.ReferencedAssemblies.Add("System.Web.Services.dll");
            parameters1.ReferencedAssemblies.Add("System.Data.dll");
            CompilerResults results1 = new CSharpCodeProvider().CompileAssemblyFromDom(parameters1, new CodeCompileUnit[] { unit1 });
            if (results1.Errors.HasErrors)
            {
                StringBuilder builder1 = new StringBuilder();
                foreach (CompilerError error1 in results1.Errors)
                {
                    builder1.Append(error1.ToString());
                    builder1.Append(Environment.NewLine);
                }
                throw new Exception(builder1.ToString());
            }
            Assembly assembly = results1.CompiledAssembly;
            Type type1 = assembly.GetType(ns + "." + classname, true, true);
            ret = Activator.CreateInstance(type1);
            System.Web.Services.Protocols.WebClientProtocol retWs = (System.Web.Services.Protocols.WebClientProtocol)ret;
            retWs.Timeout = 600000;
            //Plus soap head
            if (soapHeader != null)
            {
                string soapClassName = soapHeader["ClassName"].ToString();
                //start Soap head 
                FieldInfo client = type1.GetField(soapClassName + "Value");

                //Gets the client authentication object   
                Type typeClient = assembly.GetType(ns + "." + soapClassName);

                //Assign a value to the validation object   
                object clientkey = Activator.CreateInstance(typeClient);

                Dictionary<string, object> headerFields = soapHeader["Fields"] as Dictionary<string, object>;
                foreach (string key in headerFields.Keys)
                {
                    typeClient.GetField(key).SetValue(clientkey, headerFields[key]);
                }
                client.SetValue(retWs, clientkey);

                //Set timeout 30 seconds to prevent programs such as EFP always do not come back, resulting in request blocking
                // (ret as System.Web.Services.Protocols.SoapHttpClientProtocol) .Timeout = 30 * 1000;
                // EFP is not using ws, but directly with oracle database, so temporarily canceled, so disturb the program, there is a need to come back

            }
            return retWs;
        }

        public Type GetType<T>(string objectType, string dll, ref string dyDll, bool tryT) where T : class
        {
            Type t = null;
            string tType = tryT && typeof(T) != typeof(object) && typeof(T).FullName != objectType && !typeof(T).IsInterface && !typeof(T).IsAbstract ? typeof(T).FullName : null;
            
            if (_dyDllConfig != null)     //There are specified dyDll, then to App_Data / Config / Dll under the search, and there must be type, do not try T (/ / Has specified dyDll, then to App_Data / Config / Dll under the search, and there must be type, do not try T)
            {
                AppDomain currentDomain = AppDomain.CurrentDomain;
                //Assembly[] asses = null;
                string[] files = System.IO.Directory.GetFiles(_dyDllConfig.getPath(), "*.dll");

                for (int i = 0; i < files.Length; i++)
                {
                    /*Assembly dynamicAss = Assembly.LoadFrom(files[i]);
                    t = dynamicAss.GetType(objectType,false);
                    if (t != null)
                        break;*/
                    byte[] assemblyBytes = File.ReadAllBytes(files[i]);
                    Assembly dynamicAss = currentDomain.Load(assemblyBytes);
                    t = dynamicAss.GetType(objectType, false);
                    if (t != null)
                        break;

                }

            }
            else
            {
                Assembly[] asses = System.AppDomain.CurrentDomain.GetAssemblies();
                Dictionary<string, int> repeatAsses = new Dictionary<string, int>();
                foreach (Assembly ass in asses)
                {
                    if (!ass.GlobalAssemblyCache)
                    {
                        if (!repeatAsses.ContainsKey(ass.FullName))
                            repeatAsses.Add(ass.FullName, 0);
                        repeatAsses[ass.FullName]++;
                    }
                }
                foreach (Assembly ass in asses)
                {
                    //Not find. Net itself as a service object?
                    if (!ass.GlobalAssemblyCache)
                    {
                        t = ass.GetType(objectType, false);
                        if (t != null)
                        {
                            // TODO: Exclude those who are dynamic dll, but can not find, that obsolete, a new version
                            // TODO: If you need to reference in the app or dll third-party dll, do not want it into the bin directory, it is possible
                            // But with the version number, keep consistent

                            // This is usually used to find the way, if not cache, expired, there should be the latest version of the
                            if (repeatAsses[ass.FullName] == 1)
                                break;
                        }
                        else if (tType != null)
                        {
                            t = ass.GetType(tType, false);
                            if (t != null)
                            {
                                if (repeatAsses[ass.FullName] == 1)
                                    break;
                            }
                        }
                    }
                }
            }

            if (t == null && tType != null)
                t = typeof(T);
            return t;
        }

        IEnumerable<Type> getTypes(string filePath, Type baseType)
        {
            Assembly a = Assembly.LoadFrom(filePath);
            return a.GetTypes().Where(t => t.IsSubclassOf(baseType) && !t.IsAbstract);
        }

        object[] objectCfg(string objectID)
        {
            string key = objectID;
            Dictionary<string, object> cfg = _config.Parse<Dictionary<string, object>>(key);
            if (cfg != null)
            {
                if (cfg.ContainsKey("$ref"))
                {
                    cfg = _config.Parse<Dictionary<string, object>>(cfg["$ref"].ToString());
                }
                return new object[] { 
                    cfg.ContainsKey("Type")?cfg["Type"].ToString():null
                    ,cfg.ContainsKey("Dll")?cfg["Dll"].ToString():null
                    ,cfg.ContainsKey("Args")?(ArrayList)cfg["Args"]:null
                    ,cfg.ContainsKey("Url")?cfg["Url"].ToString():null
                    ,cfg.ContainsKey("SoapHeader")?(Dictionary<string,object>)cfg["SoapHeader"]:null
                    ,cfg.ContainsKey("DyDll")?cfg["DyDll"].ToString():null
                    ,cfg.ContainsKey("Fields")?(Dictionary<string,object>)cfg["Fields"]:null
                    //Do not make aliases, it is the first time to create an object with the alias will fail, it is recommended to store the Config object as a database
                };
            }
            return null;
        }

        Dictionary<string, object> _stores = new Dictionary<string, object>();

        public object Register(object o)
        {
            return this.Register(o.GetType().FullName, o, null);
        }

        static Dictionary<string, int> _objectCounts = new Dictionary<string, int>();
        static object _lockObj = new object();
        static object _lockDyDllObj = new object();
        public object Register(string objectID, object o, string dyDll)
        {
            lock (_lockObj)
            {
                if (!_stores.ContainsKey(objectID))
                {
                    _stores.Add(objectID, o);
                    
                    if(LogHelper.Instance.GetLogLevel()==LogHelper.LogLevel.High)
                        Tool.Info("Registers the objects in the ObjectFactory", "objectID", objectID);
                    if (_objectCounts.ContainsKey(objectID))
                    {
                        if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                            Tool.Trace("The object has been generated many times", "objectID", objectID, "frequency", _objectCounts[objectID]);
                        _objectCounts[objectID]++;
                    }
                    else
                    {
                        _objectCounts[objectID] = 1;
                    }

                    if (dyDll != null && dyDll.Length > 0)
                    {
                        lock (_lockDyDllObj)
                        {
                            if (!this._dyDllObjects.ContainsKey(dyDll))
                            {
                                this._dyDllObjects.Add(dyDll, new List<string>());
                            }
                            _dyDllObjects[dyDll].Add(objectID);
                            if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                                Tool.Info("Registering an Object relies on dynamic DLLs", "objectID", objectID, "dyDll", dyDll);
                        }
                    }
                }
                else
                {
                    if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                    {
                        Tool.Warn("ObjectFactory object already exists (to abandon the object just created)", "objectID", objectID);
                    }
                }
                o = _stores[objectID];
            }
            return o;
        }

        public string GetObjectName(object o)
        {
            foreach (string key in _stores.Keys)
            {
                if (_stores[key] == o)
                    return key;
            }
            return null;
        }

        static object _lockGetLockObjId = new object();
        static Dictionary<string, object> _lockID = new Dictionary<string, object>();

        static object getLockObj(string objectID)
        {
            lock (_lockGetLockObjId)
            {
                if (!_lockID.ContainsKey(objectID))
                {
                    _lockID.Add(objectID, new object());
                }
                return _lockID[objectID];
            }
        }


    }
}
