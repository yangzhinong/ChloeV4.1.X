using Chloe.Reflection.Emit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;

namespace Chloe.Mapper.Activators
{
    public class CollectionObjectActivator : IObjectActivator
    {
        private Type _collectionType;
        private Func<object> _activator;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<object>> ActivatorCache = new System.Collections.Concurrent.ConcurrentDictionary<Type, Func<object>>();

        private static Func<object> GetActivator(Type collectionType)
        {
            Func<object> activator = ActivatorCache.GetOrAdd(collectionType, type =>
           {
               var typeDefinition = type.GetGenericTypeDefinition();
               Type implTypeDefinition = null;
               if (typeDefinition.IsAssignableFrom(typeof(List<>)))
               {
                   implTypeDefinition = typeof(List<>);
               }
               else if (typeDefinition.IsAssignableFrom(typeof(Collection<>)))
               {
                   implTypeDefinition = typeof(Collection<>);
               }
               else
               {
                   throw new NotSupportedException($"Not supported collection type '{type.Name}'");
               }

               return DelegateGenerator.CreateInstanceActivator(implTypeDefinition.MakeGenericType(type.GetGenericArguments()[0]));
           });

            return activator;
        }

        public CollectionObjectActivator(Type collectionType)
        {
            this._collectionType = collectionType;
            this._activator = GetActivator(collectionType);
        }

        public void Prepare(IDataReader reader)
        {
        }

        public object CreateInstance(IDataReader reader)
        {
            return this._activator();
        }
    }
}