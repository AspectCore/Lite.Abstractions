﻿using System;
using System.Reflection;

namespace AspectCore.Extensions.Reflection
{
    public partial class PropertyReflector
    {
        /// <summary>
        /// 通过PropertyInfo对象和调用方式获取对应的PropertyReflector对象
        /// </summary>
        /// <param name="reflectionInfo">属性</param>
        /// <param name="callOption">调用方式</param>
        /// <returns>属性反射调用</returns>
        internal static PropertyReflector Create(PropertyInfo reflectionInfo, CallOptions callOption)
        {
            if (reflectionInfo == null)
            {
                throw new ArgumentNullException(nameof(reflectionInfo));
            }
            return ReflectorCacheUtils<Pair<PropertyInfo, CallOptions>, PropertyReflector>.GetOrAdd(new Pair<PropertyInfo, CallOptions>(reflectionInfo, callOption), CreateInternal);

            PropertyReflector CreateInternal(Pair<PropertyInfo, CallOptions> item)
            {
                var property = item.Item1;
                if (property.DeclaringType.GetTypeInfo().ContainsGenericParameters)
                {
                    return new OpenGenericPropertyReflector(property);
                }
                if ((property.CanRead && property.GetMethod.IsStatic) || (property.CanWrite && property.SetMethod.IsStatic))
                {
                    return new StaticPropertyReflector(property);
                }
                if (property.DeclaringType.GetTypeInfo().IsValueType || item.Item2 == CallOptions.Call)
                {
                    return new CallPropertyReflector(property);
                }

                return new PropertyReflector(property);
            }
        }
    }
}