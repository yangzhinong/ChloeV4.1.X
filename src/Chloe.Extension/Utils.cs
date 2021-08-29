using Chloe.Infrastructure;
using Chloe.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Chloe.Extension
{
    internal static class Utils
    {
        public static Task<T> MakeTask<T>(Func<T> func)
        {
#if net40
            T result = func();
            var task = new Task<T>(() => { return result; });
            task.Start();
            return task;
#else
            return Task.FromResult(func());
#endif
        }

        public static DbParam[] BuildParams(IDbContext dbContext, object parameter)
        {
            return PublicHelper.BuildParams((DbContext)dbContext, parameter);
        }
    }
}