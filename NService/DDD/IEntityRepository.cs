using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NService.DDD
{
    interface IEntityRepository
    {
        void Delete(string type, string id);
        T Get<T>(string id) where T : Entity;
        //純粹用于查詢
        //List<T> GetList<T>(List<string> ids) where T : Entity;
        bool Exists(string type, string id);

        void Insert(Entity entity);
        void Update(Entity entity);

        void Lock(string type, string id);
        void UnLock(string type, string id);
    }
}
