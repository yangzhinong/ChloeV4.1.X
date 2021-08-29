using Chloe.Entity;
using Chloe.Reflection;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace Chloe.Descriptors
{
    public class PropertyDescriptor
    {
        private MemberValueGetter _valueGetter;
        private MemberValueSetter _valueSetter;

        protected PropertyDescriptor(PropertyDefinition definition, TypeDescriptor declaringTypeDescriptor)
        {
            this.Definition = definition;
            this.DeclaringTypeDescriptor = declaringTypeDescriptor;
        }

        public PropertyDefinition Definition { get; private set; }
        public TypeDescriptor DeclaringTypeDescriptor { get; private set; }
        public PropertyInfo Property { get { return this.Definition.Property; } }
        public Type PropertyType { get { return this.Definition.Property.PropertyType; } }

        public object GetValue(object instance)
        {
            if (null == this._valueGetter)
            {
                this._valueGetter = MemberValueGetterContainer.GetMemberValueGetter(this.Definition.Property);
            }

            return this._valueGetter(instance);
        }

        public void SetValue(object instance, object value)
        {
            if (null == this._valueSetter)
            {
                this._valueSetter = MemberValueSetterContainer.GetMemberValueSetter(this.Definition.Property);
            }

            this._valueSetter(instance, value);
        }

        public bool HasAnnotation(Type attributeType)
        {
            return this.Definition.Annotations.Any(a => a.GetType() == attributeType);
        }
    }
}