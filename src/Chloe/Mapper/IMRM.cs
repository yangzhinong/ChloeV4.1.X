using Chloe.Data;
using Chloe.Reflection;
using Chloe.Reflection.Emit;
using System;
using System.Data;
using System.Reflection;
using System.Threading;
using MappingType = Chloe.Infrastructure.MappingType;

namespace Chloe.Mapper
{
    public interface IMRM
    {
        void Map(object instance, IDataReader reader, int ordinal);
    }

    public class MRMTuple
    {
        public Lazy<IMRM> StrongMRM { get; set; }
        public Lazy<IMRM> SafeMRM { get; set; }
    }

    internal static class MRMHelper
    {
        public static IMRM CreateMRM(MemberInfo member, MappingType mappingType)
        {
            Type type = ClassGenerator.CreateMRMType(member);
            IMRM obj = (IMRM)type.GetConstructor(Type.EmptyTypes).Invoke(null);
            return obj;
        }

        public static MRMTuple CreateMRMTuple(MemberInfo member, MappingType mappingType)
        {
            MRMTuple mrmTuple = new MRMTuple();

            mrmTuple.StrongMRM = new Lazy<IMRM>(() =>
            {
                return new MRM(member);
                //Type type = ClassGenerator.CreateMRMType(member);
                //IMRM strongMrm = (IMRM)type.GetDefaultConstructor().Invoke(null);
                //return strongMrm;
            }, LazyThreadSafetyMode.ExecutionAndPublication);

            if (member.GetMemberType().GetUnderlyingType().IsEnum /* 枚举比较特殊 */)
            {
                mrmTuple.SafeMRM = mrmTuple.StrongMRM;
            }
            else
            {
                mrmTuple.SafeMRM = new Lazy<IMRM>(() =>
                {
                    return new MRM2(member, mappingType);
                }, LazyThreadSafetyMode.ExecutionAndPublication);
            }

            return mrmTuple;
        }
    }

    internal class MRM : IMRM
    {
        private Action<object, IDataReader, int> _mapper;

        public MRM(MemberInfo member)
        {
            this._mapper = DelegateGenerator.CreateSetValueFromReaderDelegate(member);
        }

        public void Map(object instance, IDataReader reader, int ordinal)
        {
            this._mapper(instance, reader, ordinal);
        }
    }

    internal class MRM2 : IMRM
    {
        private MemberValueSetter _valueSetter;
        private MappingType _mappingType;

        public MRM2(MemberInfo member, MappingType mappingType)
        {
            this._mappingType = mappingType;
            this._valueSetter = MemberValueSetterContainer.GetMemberValueSetter(member);
        }

        public void Map(object instance, IDataReader reader, int ordinal)
        {
            object value = DataReaderExtension.GetValue(reader, ordinal);
            if (value == null)
            {
                this._valueSetter(instance, null);
                return;
            }

            value = this._mappingType.DbValueConverter.Convert(value);
            this._valueSetter(instance, value);
        }
    }
}