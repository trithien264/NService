using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace NService.Tools
{
    /// <summary>
    /// 對JSON對象的封裝
    /// </summary>
    public class AssemblyParser : IParser
    {
        static AssemblyParser()
        {
            //_dynamicLoadNames = new List<string>();
            DynamicDllRefs = new Dictionary<string, List<string>>();
            //AppDomain.CurrentDomain.AssemblyLoad += new AssemblyLoadEventHandler(CurrentDomain_AssemblyLoad);
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
        }

        //static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        //{
        //    Tool.Info("CurrentDomain_AssemblyLoad", "dllName", args.LoadedAssembly.GetName().Name);//, "dll", args.Name);
        //}

        //static List<string> _dynamicLoadNames;
        public static Dictionary<string, List<string>> DynamicDllRefs;

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        { 
            string dllName = args.Name.Substring(0, args.Name.IndexOf(','))  + ".dll.service";    
            //string dllPath = SystemConfig.Instance.Config.ConfigPath("Dll") + "\\" + dllName;
            //web service動態產生的dll
            if(dllName.IndexOf("XmlSerializers") < 0)
            {
                if (LogHelper.Instance.GetLogLevel() == LogHelper.LogLevel.High)
                    Tool.Warn("Dynamic dll has to continue to reference dynamic dll, but please note that the latter update, the former will not automatically update ", "dllName", dllName);//, "dll", args.Name);
            }
                
            Assembly ret = ObjectFactory.Instance.DyConfig.Parse<Assembly>(dllName);
            //AssemblyName[] refNames = ret.GetReferencedAssemblies();
            //_dynamicLoadNames.Add(dllName);
            return ret;
        }

        public string Kind
        {
            get
            {
                return "2";     //以path來解析
            }
        }

        public byte[] readPdb(string path)
        {
            string pdbpath = path.ToLower().Replace(".dll.service", ".pdb").Replace(".dll", ".pdb");
            if(File.Exists(pdbpath)){
                byte[] bytes = null;
                using (FileStream f = new FileStream(pdbpath, FileMode.Open, FileAccess.Read))
                {
                    //public struct Int32 
                    //表示 System.Int32 的最大可能值。這個欄位是常數。
                    //public const int MaxValue = 2147483647; //約2G
                    bytes = new byte[f.Length];
                    if (f.Length > int.MaxValue)
                    {
                        throw new ApplicationException("暫不支援超過2G的dll載入:" + path);
                        /*
                        long remainLen = f.Length;
                        int batchSize = 1024 * 1024;// int.MaxValue;
                        while (remainLen > 0)
                        {
                            //第2個參數是int型的，但是當前位置可能已經是long了，微軟的BUG?
                            f.Read(bytes, 0, remainLen > batchSize ? batchSize : (int)remainLen);
                            remainLen -= batchSize;
                        }
                        */
                    }
                    else
                    {
                        //暫不支援超過2G的文件
                        f.Read(bytes, 0, (int)f.Length);        //可能文件太大，超過int的大小，所以要分批讀
                    }
                }
                return bytes;
            }
            return null;
        }
 
        public T Read<T>(string path) where T : class
        {
            if (typeof(T) == typeof(System.Reflection.Assembly))
            {
                using(FileStream f = new FileStream(path,FileMode.Open,FileAccess.Read))
                {
                    //public struct Int32 
                    //表示 System.Int32 的最大可能值。這個欄位是常數。
                    //public const int MaxValue = 2147483647; //約2G
                    byte[] bytes = new byte[f.Length];
                    if (f.Length > int.MaxValue)
                    {
                        throw new ApplicationException("暫不支援超過2G的dll載入:" + path);
                        /*
                        long remainLen = f.Length;
                        int batchSize = 1024 * 1024;// int.MaxValue;
                        while (remainLen > 0)
                        {
                            //第2個參數是int型的，但是當前位置可能已經是long了，微軟的BUG?
                            f.Read(bytes, 0, remainLen > batchSize ? batchSize : (int)remainLen);
                            remainLen -= batchSize;
                        }
                        */
                    }
                    else
                    {
                        //暫不支援超過2G的文件
                        f.Read(bytes,0,(int)f.Length);        //可能文件太大，超過int的大小，所以要分批讀
                    }
                    //如何移除？TODO:可能需要用.net特有的版本管控來完成，否則則會有問題
                    try
                    {
                        byte[] pdbs = readPdb(path);        //偵錯檔載入
                        Assembly ret = null;
                        if (pdbs != null)
                        {
                            ret = Assembly.Load(bytes, pdbs);
                        }
                        else
                        {
                            ret = Assembly.Load(bytes);  
                        }

                        //記錄這個dll被哪些dll引用了
                        /*string thisDllName = ret.FullName.Substring(0, ret.FullName.IndexOf(',')) + ".dll.service";
                        Tool.Info("Dynamic Parse dll", "path", path, "FullName", ret.FullName);
                        AssemblyName[] refNames = ret.GetReferencedAssemblies();
                        foreach (AssemblyName ass in refNames)
                        { 
                            string assName = ass.FullName.Substring(0, ass.FullName.IndexOf(',')) + ".dll.service";
                            string dllPath = SystemConfig.Instance.Config.ConfigPath("Dll") + "\\" + assName;
                            if (File.Exists(dllPath))
                            //if (_dynamicLoadNames.Contains(assName))
                            {
                                if (!DynamicDllRefs.ContainsKey(assName))
                                    DynamicDllRefs.Add(assName, new List<string>());
                                if(!DynamicDllRefs[assName].Contains(thisDllName))
                                    DynamicDllRefs[assName].Add(thisDllName);
                                Tool.Info("record dynamic dll reference relation", "refed dll", assName, "ref dll", thisDllName);
                            }
                        }*/
                        return ret as T;
                    }
                    catch (Exception ex)
                    {
                        
                        Tool.Error("Dynamic parsing dll.server failed: ", "path", path, "ex", ex);
                        throw new ApplicationException("dll動態解析失敗:" + path + ",ex:" + ex.Message);
                    }
                }
                //用文件不能動態更新   
                /*
                string newPath = path + ".tmp";
                System.IO.File.Copy(path, newPath, true);   
                return Assembly.LoadFile(newPath) as T;     //path和assembly相關聯，不允許動了，所以采用臨時文件
                */
            }
            throw new ApplicationException("AssemblyParser can only parse System.Reflection.Assembly:" + path);
        }

    }
}