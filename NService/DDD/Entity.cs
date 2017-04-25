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


namespace NService.DDD
{
    public class Entity
    {
        public Entity() :
            this(Guid.NewGuid().ToString())
        {

        }

        public Entity(string id)
        {
           
            this.id = id;
            //這里面會用到id，所以必須在這之前賦值id
            //要不Add不用到id
            //還是要，因為要被后面的可以Get到
            //this.id = Guid.NewGuid().ToString();
           
        }

        public virtual void Destroy()
        {
         
        }

        string id;

        //protected virtual string initID()        //需要命名ID的可以在這里做
        //{
        //}

        //如果中間有.，則表示可依集合進行刪除
        public string ID
        {
            get
            {
                //if (this.id == null || this.id.Length == 0)
                //    this.id = initID();
                return this.id;
            }
        }
    }
}