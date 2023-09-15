using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TriHard.Benchmarks
{
    public static class TypeExtensionMethods
    {

        public static string GetNameOrFirstGenericType(this Type type)
        {
            var genericTypes = type.GetGenericArguments();
            if (genericTypes.Any())
            {
                var name = genericTypes[0].Name;
                var indexOfGenericTypeDelimiter = name.IndexOf('`');
                if (indexOfGenericTypeDelimiter > 0)
                {
                    name = name.Substring(0, indexOfGenericTypeDelimiter);
                }
                return name;
            }
            else
            {
                return type.Name;
            }
        }
    }
}
