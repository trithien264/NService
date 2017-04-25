using NService.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NService
{
    public enum AccessType
    {
        AllCanCall,         //Including the rights, the service of the service call,
        LoginUser
    }

    public class RightsAccessAttribute : Attribute
    {
        public static readonly RightsAccessAttribute Instance = new RightsAccessAttribute();
        AccessType _accessType;

        public RightsAccessAttribute()
        {
        }
        public RightsAccessAttribute(AccessType rightType)
        {
            _accessType = rightType;
        }

        //ui no need check permission
        public static bool HasUi(string t)
        {
            if (t.StartsWith("Apps.ui"))
                return true;
            return false;
        }

        public bool HasRights(object t, string command)
        {
            if (t.GetType().IsClass)//Before check no permission class
            {
                object[] laClassAttributes = t.GetType().GetCustomAttributes(typeof(RightsAccessAttribute), false);
                if (laClassAttributes.Length>0)
                    return CheckAttribute(t,laClassAttributes);  

            }

            //After check no permission method
            System.Reflection.MethodInfo met = t.GetType().GetMethod(command);
            if (met != null)
            {
                object[] laMethodAttributes = met.GetCustomAttributes(typeof(RightsAccessAttribute), false);
                if (laMethodAttributes.Length>0)
                    return CheckAttribute(t, laMethodAttributes);
                //ui no need check permission and not attribute login
                if (HasUi(t.ToString()))              
                    return true;
               
            }        
            return false;
        }

        private bool CheckAttribute(object t,object[] ArrAttr)
        {            
            for (int i = 0; i < ArrAttr.Length; i++)
            {
                RightsAccessAttribute arr = (RightsAccessAttribute)ArrAttr[i];
                if (arr._accessType == AccessType.AllCanCall)
                    return true;
                else if(arr._accessType == AccessType.LoginUser)
                {                    
                    if(!string.IsNullOrEmpty(AuthenticateHelper.Instance.UserID))
                        return true;
                }

            }
            return false;
        }




        /*public interface IRightsAccess
        {      
            T Get<T>() where T : class;

        }

        public class AllCanCall : RightsAccessAttribute
        {        
        }

        public class Login : RightsAccessAttribute
        {
        }

        public class RightsAccessAttribute:Attribute
        {
       

            public static readonly RightsAccessAttribute Instance = new RightsAccessAttribute();
            public static RightsAccessAttribute AllCanCall
            {
                get
                {
                    return ObjectFactory.Instance.Get<AllCanCall>();
                }
            }

            public static RightsAccessAttribute Login
            {
                get
                {
                    return ObjectFactory.Instance.Get<Login>();
                }
            }


            public static bool Get(object t, string command)
            {     
          


                return false;
            }
        }*/

        //public class AllCanCallAttribute : Attribute
        //{
        //}
        //public class clsAllCanCall
        //{

        //    public new static readonly clsAllCanCall Instance = new clsAllCanCall();
        //    public clsAllCanCall()
        //    {           
        //    }

        //    public static bool isAllCanCall(object t, string command)
        //    {     
        //        if(t.GetType().IsClass)//Before check no permission class
        //        {
        //            object[] laMethodAttributes = t.GetType().GetCustomAttributes(typeof(AllCanCallAttribute), false);
        //            if (laMethodAttributes.Length > 0)
        //                return true;               
        //        }

        //        //After check no permission method
        //        System.Reflection.MethodInfo met = t.GetType().GetMethod(command);
        //        if (met != null)
        //        {
        //            object[] laMethodAttributes = met.GetCustomAttributes(typeof(AllCanCallAttribute), false);
        //            if (laMethodAttributes.Length > 0)
        //                return true;
        //        }


        //        #region Old check method
        //        /*MethodInfo[] laMethods = t.GetType().GetMethods(
        //            BindingFlags.Public |
        //            BindingFlags.Instance |
        //            BindingFlags.DeclaredOnly);

        //        for (int i = 0; i < laMethods.Length; i++)
        //        {
        //            if (laMethods[i].Name.ToLower() == command.ToLower())
        //            {
        //                object[] laMethodAttributes = laMethods[i].GetCustomAttributes(typeof(AllCanCallAttribute), false);
        //                if (laMethodAttributes.Length > 0)
        //                    return true;
        //            }
        //        }*/            
        //        #endregion



        //       return false;
        //    }

        //}

    }
}
