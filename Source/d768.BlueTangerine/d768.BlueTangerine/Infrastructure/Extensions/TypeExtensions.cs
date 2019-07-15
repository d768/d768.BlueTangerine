using System;
using System.Reflection;

namespace d768.BlueTangerine.Infrastructure.Extensions
{
    public static class TypeExtensions
    {
        public static bool IsDefaultValue(this Type type, object value)
        {
            if (value == null)
                return true;
            return value.Equals(type.GetDefaultValue());
        }
        
        public static object GetDefaultValue(this Type type)
        {
            if (!type.GetTypeInfo().IsValueType)
                return (object) null;
            object obj;

            return Activator.CreateInstance(type);
        }
    }
}