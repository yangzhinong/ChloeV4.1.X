using Chloe.Extensions;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Chloe.Core.Visitors
{
    public class InitMemberExtractor : ExpressionVisitor<Dictionary<MemberInfo, Expression>>
    {
        private static readonly InitMemberExtractor _extractor = new InitMemberExtractor();

        private InitMemberExtractor()
        {
        }

        public static Dictionary<MemberInfo, Expression> Extract(Expression exp)
        {
            return _extractor.Visit(exp);
        }

        public override Dictionary<MemberInfo, Expression> Visit(Expression exp)
        {
            if (exp == null)
                return null;

            switch (exp.NodeType)
            {
                case ExpressionType.Lambda:
                    {
                        LambdaExpression lambdaExp = exp as LambdaExpression;
                        if (lambdaExp.Body is MemberExpression)
                        {
                            Dictionary<MemberInfo, Expression> ret = new Dictionary<MemberInfo, Expression>();
                            var body = lambdaExp.Body as MemberExpression;
                            var obj = lambdaExp.Compile().DynamicInvoke(new object[] { null });
                            foreach (PropertyInfo prop in body.Type.GetProperties())
                            {
                                var v = prop.GetValue(obj, null);
                                ret.Add(prop, ExpressionExtension.MakeWrapperAccess(v, prop.PropertyType));
                            }
                            return ret;
                        }
                        else
                        {
                            return this.VisitLambda((LambdaExpression)exp);
                        }
                    }

                case ExpressionType.MemberInit:
                    return this.VisitMemberInit((MemberInitExpression)exp);

                default:
                    throw new Exception(string.Format("Unhandled expression type: '{0}'", exp.NodeType));
            }
        }

        protected override Dictionary<MemberInfo, Expression> VisitLambda(LambdaExpression exp)
        {
            return this.Visit(exp.Body);
        }

        protected override Dictionary<MemberInfo, Expression> VisitMemberInit(MemberInitExpression exp)
        {
            Dictionary<MemberInfo, Expression> ret = new Dictionary<MemberInfo, Expression>(exp.Bindings.Count);

            foreach (MemberBinding binding in exp.Bindings)
            {
                if (binding.BindingType != MemberBindingType.Assignment)
                {
                    throw new NotSupportedException();
                }

                MemberAssignment memberAssignment = (MemberAssignment)binding;
                MemberInfo member = memberAssignment.Member;

                ret.Add(member, memberAssignment.Expression);
            }

            return ret;
        }
    }
}