using Chloe.Reflection.Emit;
using System.Reflection;

namespace Chloe.Reflection
{
    public class MemberValueSetterContainer
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<MemberInfo, MemberValueSetter> Cache = new System.Collections.Concurrent.ConcurrentDictionary<MemberInfo, MemberValueSetter>();

        public static MemberValueSetter GetMemberValueSetter(MemberInfo memberInfo)
        {
            MemberValueSetter setter = null;
            if (!Cache.TryGetValue(memberInfo, out setter))
            {
                lock (memberInfo)
                {
                    if (!Cache.TryGetValue(memberInfo, out setter))
                    {
                        setter = DelegateGenerator.CreateValueSetter(memberInfo);
                        Cache.GetOrAdd(memberInfo, setter);
                    }
                }
            }
            return setter;
        }
    }
}