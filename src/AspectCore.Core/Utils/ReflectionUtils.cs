﻿using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AspectCore.Extensions.Reflection;

namespace AspectCore.DynamicProxy
{
    public static class ReflectionUtils
    {
        public static bool IsProxy(this object instance)
        {
            if (instance == null)
            {
                return false;
            }
            return instance.GetType().GetTypeInfo().IsProxyType();
        }

        public static bool IsProxyType(this TypeInfo typeInfo)
        {
            if (typeInfo == null)
            {
                throw new ArgumentNullException(nameof(typeInfo));
            }
            return typeInfo.GetReflector().IsDefined(typeof(DynamicallyAttribute));
        }

        public static bool CanInherited(this TypeInfo typeInfo)
        {
            if (typeInfo == null)
            {
                throw new ArgumentNullException(nameof(typeInfo));
            }

            if (typeInfo.IsValueType || typeInfo.IsEnum || typeInfo.IsSealed || typeInfo.IsProxyType())
            {
                return false;
            }

            return typeInfo.IsVisible();
        }

        internal static Type[] GetParameterTypes(this MethodInfo method)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }
            return method.GetParameters().Select(parame => parame.ParameterType).ToArray();
        }

        /// <summary>
        /// 类型是否标记了NonAspectAttribute特性，以指明无需代理
        /// </summary>
        /// <param name="typeInfo">待判断的类型</param>
        /// <returns>是否标记了NonAspectAttribute特性</returns>
        public static bool IsNonAspect(this TypeInfo typeInfo)
        {
            if (typeInfo == null)
            {
                throw new ArgumentNullException(nameof(typeInfo));
            }
            return typeInfo.GetReflector().IsDefined(typeof(NonAspectAttribute));
        }

        /// <summary>
        /// 检查方法或方法的声明类型上是否标注了NonAspectAttribute特性
        /// </summary>
        /// <param name="methodInfo">方法</param>
        /// <returns>true 标注,false 没有标注</returns>
        public static bool IsNonAspect(this MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }
            return methodInfo.DeclaringType.GetTypeInfo().IsNonAspect() || methodInfo.GetReflector().IsDefined(typeof(NonAspectAttribute));
        }

        internal static bool IsCallvirt(this MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }
            if (methodInfo.IsExplicit())
            {
                return true;
            }
            var typeInfo = methodInfo.DeclaringType.GetTypeInfo();
            if (typeInfo.IsClass)
            {
                return false;
            }
            return true;
        }

        internal static bool IsExplicit(this MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }
            return methodInfo.Attributes.HasFlag(MethodAttributes.Private | MethodAttributes.Final |
                                                 MethodAttributes.Virtual);
        }

        internal static bool IsVoid(this MethodInfo methodInfo)
        {
            return methodInfo.ReturnType == typeof(void);
        }

        /// <summary>
        /// 获取以特定字符串表示的属性显示名称
        /// </summary>
        /// <param name="member">属性</param>
        /// <returns>以特定字符串表示的属性显示名称</returns>
        internal static string GetDisplayName(this PropertyInfo member)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }
            var declaringType = member.DeclaringType.GetTypeInfo();
            if (declaringType.IsInterface)
            {
                return $"{declaringType.Namespace}.{declaringType.GetReflector().DisplayName}.{member.Name}";
            }
            return member.Name;
        }

        /// <summary>
        /// 获取以特定字符串表示的方法名
        /// </summary>
        /// <param name="member">方法</param>
        /// <returns>特定字符串表示的方法名</returns>
        internal static string GetName(this MethodInfo member)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }
            var declaringType = member.DeclaringType.GetTypeInfo();
            if (declaringType.IsInterface)
            {
                return $"{declaringType.Namespace}.{declaringType.GetReflector().DisplayName}.{member.Name}";
            }
            return member.Name;
        }

        /// <summary>
        /// 获取以特定字符串表示的方法显示名称
        /// </summary>
        /// <param name="member">方法</param>
        /// <returns>特定字符串表示的方法显示名称</returns>
        internal static string GetDisplayName(this MethodInfo member)
        {
            if (member == null)
            {
                throw new ArgumentNullException(nameof(member));
            }
            var declaringType = member.DeclaringType.GetTypeInfo();
            return
                $"{declaringType.Namespace}.{declaringType.GetReflector().DisplayName}.{member.GetReflector().DisplayName}";
        }

        /// <summary>
        /// 判断方法的返回值类型是否为Task<>类型
        /// </summary>
        /// <param name="methodInfo">方法</param>
        /// <returns>true 表示返回值为Task<>类型,否则返回false</returns>
        public static bool IsReturnTask(this MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }
            var returnType = methodInfo.ReturnType.GetTypeInfo();
            return returnType.IsTaskWithResult();
        }

        /// <summary>
        /// 判断方法的返回值类型是否为ValueTask<>类型
        /// </summary>
        /// <param name="methodInfo">方法</param>
        /// <returns>true 表示返回值为ValueTask<>类型,否则返回false</returns>
        public static bool IsReturnValueTask(this MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                throw new ArgumentNullException(nameof(methodInfo));
            }
            var returnType = methodInfo.ReturnType.GetTypeInfo();
            return returnType.IsValueTaskWithResult();
        }

        /// <summary>
        /// 属性可见且为虚方法,则返回true,否则返回false
        /// </summary>
        /// <param name="property">属性</param>
        /// <returns>属性可见且为虚方法,则返回true,否则返回false</returns>
        public static bool IsVisibleAndVirtual(this PropertyInfo property)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }
            return (property.CanRead && property.GetMethod.IsVisibleAndVirtual()) ||
                   (property.CanWrite && property.GetMethod.IsVisibleAndVirtual());
        }

        /// <summary>
        /// 方法可见且为虚方法,则返回true,否则返回false
        /// </summary>
        /// <param name="method">方法</param>
        /// <returns>方法可见且为虚方法,则返回true,否则返回false</returns>
        public static bool IsVisibleAndVirtual(this MethodInfo method)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }
            if (method.IsStatic || method.IsFinal)
            {
                return false;
            }
            return method.IsVirtual &&
                    (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);
        }

        public static MethodInfo GetMethodBySignature(this TypeInfo typeInfo, MethodInfo method)
        {
            if (typeInfo == null)
            {
                throw new ArgumentNullException(nameof(typeInfo));
            }
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }
            var methods = typeInfo.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var displayName = method.GetReflector().DisplayName;
            var invocation = methods.FirstOrDefault(x => x.GetReflector().DisplayName.Equals(displayName, StringComparison.Ordinal));
            if (invocation != null)
            {
                return invocation;
            }
            var declaringType = method.DeclaringType;
            displayName = $"{declaringType.GetReflector().FullDisplayName}.{displayName.Split(' ').Last()}";
            invocation = methods.FirstOrDefault(x => x.GetReflector().DisplayName.Split(' ').Last().Equals(displayName, StringComparison.Ordinal));
            if (invocation != null)
            {
                return invocation;
            }
            invocation = typeInfo.GetMethodBySignature(new MethodSignature(method));
            if (invocation != null)
            {
                return invocation;
            }

            displayName = $"{declaringType.GetReflector().FullDisplayName}.{method.Name}";
            return typeInfo.GetMethodBySignature(
                new MethodSignature(method, displayName));
        }
    }
}