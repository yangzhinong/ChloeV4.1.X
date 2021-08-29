using Chloe.Data;
using Chloe.Extensions;
using Chloe.Mapper;
using Chloe.Reflection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;

namespace Chloe.Reflection.Emit
{
    public static class DelegateGenerator
    {
        public static Func<IDataReader, int, object> CreateDataReaderGetValueHandler(Type valueType)
        {
            var reader = Expression.Parameter(typeof(IDataReader), "reader");
            var ordinal = Expression.Parameter(typeof(int), "ordinal");

            var readerMethod = DataReaderConstant.GetReaderMethod(valueType);

            var getValue = Expression.Call(null, readerMethod, reader, ordinal);
            var toObject = Expression.Convert(getValue, typeof(object));

            var lambda = Expression.Lambda<Func<IDataReader, int, object>>(toObject, reader, ordinal);
            var del = lambda.Compile();

            return del;
        }

        public static Action<object, IDataReader, int> CreateSetValueFromReaderDelegate(MemberInfo member)
        {
            var p = Expression.Parameter(typeof(object), "instance");
            var instance = Expression.Convert(p, member.DeclaringType);
            var reader = Expression.Parameter(typeof(IDataReader), "reader");
            var ordinal = Expression.Parameter(typeof(int), "ordinal");

            var readerMethod = DataReaderConstant.GetReaderMethod(member.GetMemberType());
            var getValue = Expression.Call(null, readerMethod, reader, ordinal);
            var assign = ExpressionExtension.Assign(member, instance, getValue);
            var lambda = Expression.Lambda<Action<object, IDataReader, int>>(assign, p, reader, ordinal);

            Action<object, IDataReader, int> del = lambda.Compile();

            return del;
        }

        public static InstanceCreator CreateInstanceCreator(ConstructorInfo constructor)
        {
            PublicHelper.CheckNull(constructor);

            var pExp_reader = Expression.Parameter(typeof(IDataReader), "reader");
            var pExp_argumentActivators = Expression.Parameter(typeof(List<IObjectActivator>), "argumentActivators");
            var getItemMethod = typeof(List<IObjectActivator>).GetMethod("get_Item");

            ParameterInfo[] parameters = constructor.GetParameters();
            List<Expression> arguments = new List<Expression>(parameters.Length);

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];

                //IObjectActivator oa = argumentActivators[i];
                var oa = Expression.Call(pExp_argumentActivators, getItemMethod, Expression.Constant(i));
                //object obj = oa.CreateInstance(IDataReader reader);
                var obj = Expression.Call(oa, typeof(IObjectActivator).GetMethod("CreateInstance"), pExp_reader);
                //T argument = (T)obj;
                var argument = Expression.Convert(obj, parameter.ParameterType);
                arguments.Add(argument);
            }

            var body = Expression.New(constructor, arguments);
            InstanceCreator ret = Expression.Lambda<InstanceCreator>(body, pExp_reader, pExp_argumentActivators).Compile();

            return ret;
        }

        public static MemberValueSetter CreateValueSetter(MemberInfo propertyOrField)
        {
            PropertyInfo propertyInfo = propertyOrField as PropertyInfo;
            if (propertyInfo != null)
                return CreateValueSetter(propertyInfo);

            FieldInfo fieldInfo = propertyOrField as FieldInfo;
            if (fieldInfo != null)
                return CreateValueSetter(fieldInfo);

            throw new ArgumentException();
        }

        public static MemberValueSetter CreateValueSetter(PropertyInfo propertyInfo)
        {
            var p = Expression.Parameter(typeof(object), "instance");
            var pValue = Expression.Parameter(typeof(object), "value");
            var instance = Expression.Convert(p, propertyInfo.DeclaringType);
            var value = Expression.Convert(pValue, propertyInfo.PropertyType);

            var pro = Expression.Property(instance, propertyInfo);
            var setValue = Expression.Assign(pro, value);

            Expression body = setValue;

            var lambda = Expression.Lambda<MemberValueSetter>(body, p, pValue);
            MemberValueSetter ret = lambda.Compile();

            return ret;
        }

        public static MemberValueSetter CreateValueSetter(FieldInfo fieldInfo)
        {
            var p = Expression.Parameter(typeof(object), "instance");
            var pValue = Expression.Parameter(typeof(object), "value");
            var instance = Expression.Convert(p, fieldInfo.DeclaringType);
            var value = Expression.Convert(pValue, fieldInfo.FieldType);

            var field = Expression.Field(instance, fieldInfo);
            var setValue = Expression.Assign(field, value);

            Expression body = setValue;

            var lambda = Expression.Lambda<MemberValueSetter>(body, p, pValue);
            MemberValueSetter ret = lambda.Compile();

            return ret;
        }

        public static MemberValueGetter CreateValueGetter(MemberInfo propertyOrField)
        {
            var p = Expression.Parameter(typeof(object), "a");
            var instance = Expression.Convert(p, propertyOrField.DeclaringType);
            var memberAccess = Expression.MakeMemberAccess(instance, propertyOrField);

            Type type = propertyOrField.GetMemberType();

            Expression body = memberAccess;
            if (type.IsValueType)
            {
                body = Expression.Convert(memberAccess, typeof(object));
            }

            var lambda = Expression.Lambda<MemberValueGetter>(body, p);
            MemberValueGetter ret = lambda.Compile();

            return ret;
        }

        public static Func<object> CreateInstanceActivator(Type type)
        {
            var body = Expression.New(type.GetDefaultConstructor());
            var ret = Expression.Lambda<Func<object>>(body).Compile();
            return ret;
        }

        public static MethodInvoker CreateMethodInvoker(MethodInfo method)
        {
            List<ParameterExpression> parameterExps = new List<ParameterExpression>();
            ParameterExpression p = Expression.Parameter(typeof(object), "instance");
            parameterExps.Add(p);

            ParameterExpression pParameterArray = Expression.Parameter(typeof(object[]), "parameters");
            parameterExps.Add(pParameterArray);

            Expression instance = null;
            if (!method.IsStatic)
            {
                instance = Expression.Convert(p, method.ReflectedType);
            }

            ParameterInfo[] parameters = method.GetParameters();
            List<Expression> argumentExps = new List<Expression>(parameters.Length);

            var getItemMethod = typeof(object[]).GetMethod("GetValue", new Type[] { typeof(int) });

            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];

                //object parameter = parameters[i];
                var parameterExp = Expression.Call(pParameterArray, getItemMethod, Expression.Constant(i));
                //T argument = (T)parameter;
                var argumentExp = Expression.Convert(parameterExp, parameter.ParameterType);
                argumentExps.Add(argumentExp);
            }

            //instance.Method(parameters)
            MethodCallExpression methodCallExp = Expression.Call(instance, method, argumentExps);

            MethodInvoker ret;
            if (method.ReturnType == typeof(void))
            {
                var act = Expression.Lambda<Action<object, object[]>>(methodCallExp, parameterExps).Compile();
                ret = MakeMethodInvoker(act);
            }
            else
            {
                ret = Expression.Lambda<MethodInvoker>(methodCallExp, parameterExps).Compile();
                ret = MakeMethodInvoker(ret);
            }

            return ret;
        }

        private static MethodInvoker MakeMethodInvoker(Action<object, object[]> act)
        {
            MethodInvoker ret = (object instance, object[] parameters) =>
            {
                act(instance, parameters ?? ReflectionExtension.EmptyArray);
                return null;
            };

            return ret;
        }

        private static MethodInvoker MakeMethodInvoker(MethodInvoker methodInvoker)
        {
            MethodInvoker ret = (object instance, object[] parameters) =>
            {
                object val = methodInvoker(instance, parameters ?? ReflectionExtension.EmptyArray);
                return val;
            };

            return ret;
        }
    }
}