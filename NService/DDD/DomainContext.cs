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
using NService.Tools;

namespace NService.DDD
{
    public class DomainContext
    {

        const string ENTITIES_KEY_IN_CURRENT_ITEMS = "PCIWeb.DBContext";

        public static DomainContext Instace
        {
            get
            {
                if (HttpContext.Current != null)
                {
                    if (HttpContext.Current.Items.Contains(ENTITIES_KEY_IN_CURRENT_ITEMS))
                        return HttpContext.Current.Items[ENTITIES_KEY_IN_CURRENT_ITEMS] as DomainContext;
                    DomainContext ret = new DomainContext();//"TestGit.DB","ENTITY_VERSION","JSON_ENTITY");
                    HttpContext.Current.Items[ENTITIES_KEY_IN_CURRENT_ITEMS] = ret;
                    return ret;
                }
                throw new ApplicationException("DBContext only can be used on http request!");
            }
        }

        public void Lock(string type, string id)
        {
            //本來可以判斷的，但現在留在這里考察架構的穩定性，因為不可能會lock兩次同一個ID
            string key = type + "-" + id;
            if (!lockKeys.Contains(key))
            {
                lockKeys.Add(key);
                doLock(type, id);
            }
        }

        public void UnLock(string type, string id)
        {
            string key = type + "-" + id;
            if (lockKeys.Contains(key))
            {
                lockKeys.Remove(key);
                doUnLock(type, id);
            }
        }

        Dictionary<string, Entity> originEntities;
        Dictionary<string, Entity> entities;
        List<string> lockKeys;

        public DomainContext()
        {
            originEntities = new Dictionary<string, Entity>();
            entities = new Dictionary<string, Entity>();
            lockKeys = new List<string>();
        }

        public T Get<T>(string id) where T : Entity
        {
            T ret = TryGet<T>(id);
            if (ret == null)
                throw new NSInfoException("ENTITY_NULL:" + typeof(T).FullName + "-" + id);
            return ret;
        }

        public T TryGet<T>(string id) where T : Entity
        {
            string type = typeof(T).FullName;
            string key = type + "-" + id;
            if (!entities.ContainsKey(key))
            {
                if (!originEntities.ContainsKey(key))
                {
                    T entity = Retrieve<T>(id);
                    originEntities.Add(key, entity);        //不管是null還是有值，只要抓過資料庫都記錄到originEntities中
                }
                Lock(type, id);
                entities.Add(key, originEntities[key]);
            }
            return (T)entities[key];   //只要進entities，就已經lock過
        }

        public T GetRead<T>(string id) where T : Entity
        {

            T ret = TryGetRead<T>(id);
            if (ret == null)
                throw new NSErrorException("ENTITY_NULL:" + typeof(T).FullName + "-" + id);
            return ret;
        }
        public T TryGetRead<T>(string id) where T : Entity
        {
            string type = typeof(T).FullName;
            string key = type + "-" + id;
            if (!entities.ContainsKey(key))     //entities優先，代表當前狀態（修改，新增，刪除的對象）
            {
                if (!originEntities.ContainsKey(key))
                {
                    T entity = Retrieve<T>(id);
                    originEntities.Add(key, entity);        //不管是null還是有值，只要抓過資料庫都記錄到originEntities中
                }
                return (T)originEntities[key];
            }
            else
                return (T)entities[key];
        }

        public bool Exists<T>(string id)
        {
            string type = typeof(T).FullName;
            string key = type + "-" + id;
            if (entities.ContainsKey(key))
                return entities[key] != null;
            else if (originEntities.ContainsKey(key))
                return originEntities[key] != null;
            return getRepository(type).Exists(type, id);
        }

        //新new的需要add進來，如果是抓出來的，不需要了，直接commit就好
        public void Add(Entity entity)
        {
            string type = entity.GetType().FullName;
            string key = type + "-" + entity.ID;
            if (entities.ContainsKey(key) && entities[key] != null)
                throw new ApplicationException(key + " has been added");
            //Lock(type,entity.ID);     //不鎖，因為不去抓一遍也鎖不住，如果有抓過，就在抓的時候鎖住了
            entities[key] = entity;
        }

        public void Remove(Entity entity)
        {
            remove(entity.GetType().FullName, entity.ID);
        }

        /*
         * 要刪除一定要先抓出來，否則可能刪除后，又新增，而這時資料庫里的資料是不會被刪除的
         * 至于需要真的只憑ID就刪除，則可以不用在DomainContext里，而是直接用Service實現就好(不要DomainContext的Commit)
         * 單筆作業，DDD一定要有對象，再根據對象來的操作
        public void Remove<T>(string id) where T:Entity
        {
            remove(typeof(T).FullName, id);
        }
        */
        void remove(string type, string id)
        {
            string key = type + "-" + id;
            //Lock(type, id);
            entities[key] = null;       //明確刪除

        }

        public void Submit()
        {
            try
            {
                foreach (string key in entities.Keys)
                {
                    int index = key.IndexOf("-");
                    string type = key.Substring(0, index);
                    string id = key.Substring(index + 1);
                    if (entities[key] == null)
                    {
                        if (!originEntities.ContainsKey(key))
                            Delete(type, id);
                        else if (originEntities[key] == null)
                        { }
                        else
                            Delete(type, id);
                    }
                    else
                    {
                        if (!originEntities.ContainsKey(key))
                            Create(entities[key]);
                        else if (originEntities[key] == null)
                            Create(entities[key]);
                        else
                            Update(entities[key]);
                    }
                }
            }
            finally
            {
                Reset();
            }
        }

        public void Reset()
        {
            //用DB時，因為Lock和Unlock都是資料庫動作，所以不用UnLock也沒關系，因為都會UnLock掉
            //foreach (string key in entities.Keys)
            for (int i = lockKeys.Count - 1; i >= 0; i--)
            {
                string key = lockKeys[i];
                //if (originEntities.ContainsKey(key))
                //{
                int index = key.IndexOf("-");
                string type = key.Substring(0, index);
                string id = key.Substring(index + 1);
                UnLock(type, id);
                //}
            }
            entities.Clear();
            originEntities.Clear();
            lockKeys.Clear();
        }

        IEntityRepository getRepository(string type)
        {
            int lastIndex = type.LastIndexOf('.');
            string objectID = type.Substring(0, lastIndex) + ".Entity." + type.Substring(lastIndex + 1);
            //if (ObjectFactory.Instance.ObjectConfig(objectID))
            IEntityRepository ret = ObjectFactory.Instance.Get<IEntityRepository>(objectID);
            if (ret == null)
                throw new ApplicationException("ENTITY_REPOSITORY_NOT_EXISTS:" + objectID);
            return ret;
            //else
            //    return defaultRepository;
        }

        public T Retrieve<T>(string id) where T : Entity
        {
            return getRepository(typeof(T).FullName).Get<T>(id);
        }

        protected void doLock(string type, string id)
        {
            getRepository(type).Lock(type, id);
            //DBHelper.Instance.Execute("Insert_" + lockTable + "@" + lockDB, Tool.ToDic("TYPE", type, "ID", id,"LOCK_USER_ID",AuthenticateHelper.Instance.UserID,"LOCK_TIME",DateTime.Now.ToString("yyyyMMddHHmmss")));
        }

        protected void doUnLock(string type, string id)
        {
            getRepository(type).UnLock(type, id);
            //DBHelper.Instance.Execute("Delete_" + lockTable + "@" + lockDB, Tool.ToDic("TYPE", type, "ID", id));
        }

        public void Delete(string type, string id)
        {
            getRepository(type).Delete(type, id);
        }

        public void Create(Entity entity)
        {
            getRepository(entity.GetType().FullName).Insert(entity);
        }

        public void Update(Entity entity)
        {
            getRepository(entity.GetType().FullName).Update(entity);
        }
    }
}
