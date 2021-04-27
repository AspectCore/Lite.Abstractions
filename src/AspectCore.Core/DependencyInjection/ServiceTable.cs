﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AspectCore.Configuration;
using AspectCore.DynamicProxy;

namespace AspectCore.DependencyInjection
{
    /// <summary>
    /// 用于内部高效存取服务
    /// </summary>
    internal class ServiceTable
    {
        /// <summary>
        /// 存储暴露的服务与对应的多个服务描述的映射（非泛型服务）
        /// </summary>
        private readonly ConcurrentDictionary<Type, LinkedList<ServiceDefinition>> _linkedServiceDefinitions;

        /// <summary>
        /// 存储暴露的服务与对应的多个服务描述的映射（泛型服务）
        /// </summary>
        private readonly ConcurrentDictionary<Type, LinkedList<ServiceDefinition>> _linkedGenericServiceDefinitions;
        private readonly IProxyTypeGenerator _proxyTypeGenerator;
        private readonly ServiceValidator _serviceValidator;

        public ServiceTable(IAspectConfiguration configuration)
        {
            var aspectValidatorBuilder = new AspectValidatorBuilder(configuration);
            _proxyTypeGenerator = new ProxyTypeGenerator(aspectValidatorBuilder);
            _serviceValidator = new ServiceValidator(aspectValidatorBuilder);
            _linkedServiceDefinitions = new ConcurrentDictionary<Type, LinkedList<ServiceDefinition>>();
            _linkedGenericServiceDefinitions = new ConcurrentDictionary<Type, LinkedList<ServiceDefinition>>();
        }

        /// <summary>
        /// 填充内部用于存放服务的字典
        /// </summary>
        /// <param name="services">服务描述对象集合</param>
        internal void Populate(IEnumerable<ServiceDefinition> services)
        {
            //一个过滤器，晒选服务不是IManyEnumerable<>类型的服务描述对象
            Func<IEnumerable<ServiceDefinition>, IEnumerable<ServiceDefinition>> filter = input => input.Where(x => !x.IsManyEnumerable());

            foreach (var service in filter(services))
            {
                if (service.ServiceType.GetTypeInfo().ContainsGenericParameters)
                {
                    var linkedGenericServices = _linkedGenericServiceDefinitions.GetOrAdd(service.ServiceType.GetGenericTypeDefinition(), _ => new LinkedList<ServiceDefinition>());
                    linkedGenericServices.Add(service);
                }
                else
                {
                    var linkedServices = _linkedServiceDefinitions.GetOrAdd(service.ServiceType, _ => new LinkedList<ServiceDefinition>());
                    linkedServices.Add(MakProxyService(service));
                }
            }
        }

        /// <summary>
        /// 是否包含此服务定义
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>是否包含</returns>
        internal bool Contains(Type serviceType)
        {
            if (ContainsLinked(serviceType))
            {
                return true;
            }
            //IsConstructedGenericType:当此属性为true时,表示可创建当前类型的实例(封闭式泛型类型); false时不能(开放式泛型类型)
            if (serviceType.IsConstructedGenericType)
            {
                switch (serviceType.GetGenericTypeDefinition())
                {
                    case Type enumerable when enumerable == typeof(IEnumerable<>):
                    case Type manyEnumerable when manyEnumerable == typeof(IManyEnumerable<>):
                        return ContainsLinked(serviceType.GetTypeInfo().GetGenericArguments()[0]) ? true : true;
                    case Type genericTypeDefinition when _linkedGenericServiceDefinitions.ContainsKey(genericTypeDefinition):
                        return true;
                    default:
                        break;
                }
            }
            return false;
        }

        /// <summary>
        /// 是否包含此服务定义
        /// </summary>
        private bool ContainsLinked(Type serviceType)
        {
            if (_linkedServiceDefinitions.ContainsKey(serviceType))
            {
                return true;
            }
            if (serviceType.IsConstructedGenericType)
            {
                if (_linkedGenericServiceDefinitions.ContainsKey(serviceType.GetGenericTypeDefinition()))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 通过服务类型尝试获取服务描述
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务描述</returns>
        internal ServiceDefinition TryGetService(Type serviceType)
        {
            if (serviceType == null)
            {
                return null;
            }
            if (_linkedServiceDefinitions.TryGetValue(serviceType, out var value))
            {
                return value.Last.Value;
            }    
            if (serviceType.IsConstructedGenericType)
            {
                switch (serviceType.GetGenericTypeDefinition())
                {
                    case Type enumerable when enumerable == typeof(IEnumerable<>):
                        return FindEnumerable(serviceType);
                    case Type enumerable when enumerable == typeof(IManyEnumerable<>):
                        return FindManyEnumerable(serviceType);
                    case Type genericTypeDefinition when _linkedGenericServiceDefinitions.TryGetValue(genericTypeDefinition, out var genericServiceDefinitions):
                        return FindGenericService(serviceType, genericServiceDefinitions);
                    default:
                        break;
                }
            }
            return null;
        }

        /// <summary>
        /// 查询内部字典中IEnumerable<>类型的服务描述
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务描述</returns>
        private ServiceDefinition FindEnumerable(Type serviceType)
        {
            if (_linkedServiceDefinitions.TryGetValue(serviceType, out var value))
            {
                return value.Last.Value;
            }
            var elementType = serviceType.GetTypeInfo().GetGenericArguments()[0];
            var elements = FindEnumerableElements(serviceType);
            var enumerableServiceDefinition = new EnumerableServiceDefintion(serviceType, elementType, elements);
            _linkedServiceDefinitions[serviceType] = new LinkedList<ServiceDefinition>(new ServiceDefinition[] { enumerableServiceDefinition });
            return enumerableServiceDefinition;
        }

        /// <summary>
        /// 查询内部字典中IManyEnumerable<>类型的服务描述
        /// </summary>
        /// <param name="serviceType">服务类型</param>
        /// <returns>服务描述</returns>
        private ServiceDefinition FindManyEnumerable(Type serviceType)
        {
            if (_linkedServiceDefinitions.TryGetValue(serviceType, out var value))
            {
                return value.Last.Value;
            }
            var elementType = serviceType.GetTypeInfo().GetGenericArguments()[0];
            var elements = FindEnumerableElements(serviceType);
            var enumerableServiceDefinition = new ManyEnumerableServiceDefintion(serviceType, elementType, elements);
            _linkedServiceDefinitions[serviceType] = new LinkedList<ServiceDefinition>(new ServiceDefinition[] { enumerableServiceDefinition });
            return enumerableServiceDefinition;
        }

        private ServiceDefinition[] FindEnumerableElements(Type serviceType)
        {
            var elementType = serviceType.GetTypeInfo().GetGenericArguments()[0];
            var services = new List<ServiceDefinition>();
            if (_linkedServiceDefinitions.TryGetValue(elementType, out var linkedServices))
            {
                services.AddRange(linkedServices);
            }
            if (elementType.IsConstructedGenericType &&
                _linkedGenericServiceDefinitions.TryGetValue(elementType.GetGenericTypeDefinition(), out var linkedGenericServices))
            {
                services.AddRange(linkedGenericServices.Select(x => MakProxyService(MakGenericService(elementType, x))).Where(x => x != null));
            }
            return services.ToArray();
        }

        private ServiceDefinition FindGenericService(Type serviceType, LinkedList<ServiceDefinition> genericServiceDefinitions)
        {
            if (_linkedServiceDefinitions.TryGetValue(serviceType, out var value))
            {
                return value.Last.Value;
            }
            var service = MakProxyService(MakGenericService(serviceType, genericServiceDefinitions.Last.Value));
            if (service == null)
            {
                return null;
            }
            _linkedServiceDefinitions.TryAdd(serviceType, new LinkedList<ServiceDefinition>(new ServiceDefinition[] { service }));
            return service;
        }

        private ServiceDefinition MakGenericService(Type serviceType, ServiceDefinition service)
        {
            switch (service)
            {
                case InstanceServiceDefinition instanceServiceDefinition:
                    return new InstanceServiceDefinition(serviceType, instanceServiceDefinition.ImplementationInstance);
                case DelegateServiceDefinition delegateServiceDefinition:
                    return new DelegateServiceDefinition(serviceType, delegateServiceDefinition.ImplementationDelegate, delegateServiceDefinition.Lifetime);
                case TypeServiceDefinition typeServiceDefinition:
                    var elementTypes = serviceType.GetTypeInfo().GetGenericArguments();
                    return new TypeServiceDefinition(serviceType, typeServiceDefinition.ImplementationType.MakeGenericType(elementTypes), typeServiceDefinition.Lifetime);
                default:
                    return null;
            }
        }

        private ServiceDefinition MakProxyService(ServiceDefinition service)
        {
            if (service == null)
            {
                return null;
            }
            if (_serviceValidator.TryValidate(service, out Type implType))
            {
                var proxyType = service.ServiceType.GetTypeInfo().IsClass
                    ? _proxyTypeGenerator.CreateClassProxyType(service.ServiceType, implType)
                    : _proxyTypeGenerator.CreateInterfaceProxyType(service.ServiceType, implType);
                return new ProxyServiceDefinition(service, proxyType);
            }
            return service;
        }
    }
}