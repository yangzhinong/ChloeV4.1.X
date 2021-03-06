using Chloe.Exceptions;
using Chloe.Mapper.Binders;
using System;
using System.Collections.Generic;
using System.Data;

namespace Chloe.Mapper.Activators
{
    public class ComplexObjectActivator : IObjectActivator
    {
        InstanceCreator _instanceCreator;
        List<IObjectActivator> _argumentActivators;
        List<IMemberBinder> _memberBinders;
        int? _checkNullOrdinal;

        public ComplexObjectActivator(InstanceCreator instanceCreator, List<IObjectActivator> argumentActivators, List<IMemberBinder> memberBinders, int? checkNullOrdinal)
        {
            this._instanceCreator = instanceCreator;
            this._argumentActivators = argumentActivators;
            this._memberBinders = memberBinders;
            this._checkNullOrdinal = checkNullOrdinal;
        }

        public void Prepare(IDataReader reader)
        {
            for (int i = 0; i < this._argumentActivators.Count; i++)
            {
                IObjectActivator argumentActivator = this._argumentActivators[i];
                argumentActivator.Prepare(reader);
            }
            for (int i = 0; i < this._memberBinders.Count; i++)
            {
                IMemberBinder binder = this._memberBinders[i];
                binder.Prepare(reader);
            }
        }
        public virtual object CreateInstance(IDataReader reader)
        {
            if (this._checkNullOrdinal != null)
            {
                if (reader.IsDBNull(this._checkNullOrdinal.Value))
                    return null;
            }

            object obj = this._instanceCreator(reader, this._argumentActivators);

            IMemberBinder memberBinder = null;
            try
            {
                int count = this._memberBinders.Count;
                for (int i = 0; i < count; i++)
                {
                    memberBinder = this._memberBinders[i];
                    memberBinder.Bind(obj, reader);
                }
            }
            catch (ChloeException)
            {
                throw;
            }
            catch (Exception ex)
            {
                PrimitiveMemberBinder binder = memberBinder as PrimitiveMemberBinder;
                if (binder != null)
                {
                    throw new ChloeException(AppendErrorMsg(reader, binder.Ordinal, ex), ex);
                }

                throw;
            }

            return obj;
        }

        public static string AppendErrorMsg(IDataReader reader, int ordinal, Exception ex)
        {
            string msg = null;
            if (reader.IsDBNull(ordinal))
            {
                msg = string.Format("Please make sure that the member of the column '{0}'({1},{2},{3}) map is nullable.", reader.GetName(ordinal), ordinal.ToString(), reader.GetDataTypeName(ordinal), reader.GetFieldType(ordinal).FullName);
            }
            else if (ex is InvalidCastException)
            {
                msg = string.Format("Please make sure that the member of the column '{0}'({1},{2},{3}) map is the correct type.", reader.GetName(ordinal), ordinal.ToString(), reader.GetDataTypeName(ordinal), reader.GetFieldType(ordinal).FullName);
            }
            else
                msg = string.Format("An error occurred while mapping the column '{0}'({1},{2},{3}). For details please see the inner exception.", reader.GetName(ordinal), ordinal.ToString(), reader.GetDataTypeName(ordinal), reader.GetFieldType(ordinal).FullName);
            return msg;
        }
    }

    public class ObjectActivatorWithTracking : ComplexObjectActivator
    {
        IDbContext _dbContext;
        public ObjectActivatorWithTracking(InstanceCreator instanceCreator, List<IObjectActivator> argumentActivators, List<IMemberBinder> memberBinders, int? checkNullOrdinal, IDbContext dbContext)
            : base(instanceCreator, argumentActivators, memberBinders, checkNullOrdinal)
        {
            this._dbContext = dbContext;
        }

        public override object CreateInstance(IDataReader reader)
        {
            object obj = base.CreateInstance(reader);

            if (obj != null)
                this._dbContext.TrackEntity(obj);

            return obj;
        }
    }
}
