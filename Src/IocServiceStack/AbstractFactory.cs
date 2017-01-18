﻿#region License
// Copyright (c) 2016 Rajeswara-Rao-Jinaga
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

namespace IocServiceStack
{
    using System;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    /// <summary>
    /// Represents abstract factory of services
    /// </summary>
    public abstract class AbstractFactory : IContainerService
    {
        private IContractObserver _observer;

        /// <summary>
        /// Get read-only ServiceMapTable object
        /// </summary>
        protected readonly ContractServiceAutoMapper ServicesMapTable;

        /// <summary>
        /// Initializes a new instance of AbstractFactory class with the specified namespaces, assemblies and strictmode
        /// </summary>
        /// <param name="namespaces">The namespaces of services</param>
        /// <param name="assemblies">The assemblies of services</param>
        /// <param name="strictMode">The value indicating whether strict mode is on or off</param>
        public AbstractFactory(string[] namespaces, Assembly[] assemblies, bool strictMode)
        {
            ServicesMapTable = new ContractServiceAutoMapper(namespaces, assemblies, strictMode);
            ServicesMapTable.Map();
        }

        /// <summary>
        /// Gets or sets factory of Subcontract of the current service
        /// </summary>
        public SubcontractFactory DependencyFactory
        {
            get; set;
        }

        /// <summary>
        /// 
        /// </summary>
        protected IContractObserver ContractObserver
        {
            get
            {
                return _observer;
            }
            set
            {
                if (_observer == null)
                {
                    _observer = value;
                    /*Set observers to all its sub contractors*/
                    if (DependencyFactory != null)
                        DependencyFactory.ContractObserver = _observer;
                }
                else
                {
                    //TODO
                    //ExceptionHelper.ThrowOverrideObserverExpection
                }
            }
        }


        public IContainerService Add<TC, TS>()
          where TC : class
          where TS : class
        {
            return Add<TC>(typeof(TS));
        }

        /// <summary>
        /// Adds the specified service to the factory
        /// </summary>
        /// <typeparam name="T">The class of the service</typeparam>
        /// <param name="service">The type of the service</param>
        /// <returns>Instance <see cref="IContainerService"/> of current object</returns>
        public virtual IContainerService Add<T>(Type service) where T : class
        {
            Type interfaceType = typeof(T);
            var serviceMeta = new ServiceInfo(service, ServiceInfo.GetDecorators(interfaceType));
            ServicesMapTable.Add(interfaceType, serviceMeta);

            //send update to observer
            ContractObserver.Update(interfaceType);

            return this;
        }

        public IContainerService AddSingleton<TC, TS>()
            where TC : class
            where TS : class
        {
            Type interfaceType = typeof(TC);
            var serviceMeta = new ServiceInfo<TS>(isReusable: true, decorators: ServiceInfo.GetDecorators(interfaceType) );

            ServicesMapTable.Add(interfaceType, serviceMeta);

            //send update to observer
            ContractObserver.Update(interfaceType);

            return this;
        }

        public IContainerService Replace<TC, TS>()
          where TC : class
          where TS : class
        {
            return Replace<TC>(typeof(TS));
        }

        /// <summary>
        /// Replaces the specified service in the factory.
        /// </summary>
        /// <typeparam name="T">The class of the service</typeparam>
        /// <param name="service"></param>
        /// <returns></returns>
        public virtual IContainerService Replace<T>(Type service) where T : class
        {
            Type interfaceType = typeof(T);
            ServicesMapTable[interfaceType] = new ServiceInfo(service, ServiceInfo.GetDecorators(interfaceType));

            //send update to observer
            ContractObserver.Update(interfaceType);

            return this;
        }

        public IContainerService ReplaceSingleton<TC, TS>()
            where TC : class
            where TS : class
        {
            Type interfaceType = typeof(TC);
            var serviceMeta = new ServiceInfo<TS>(isReusable:true, decorators: ServiceInfo.GetDecorators(interfaceType));

            ServicesMapTable[interfaceType] = serviceMeta;

            //send update to observer
            ContractObserver.Update(interfaceType);

            return this;
        }

        protected Expression CreateConstructorExpression(Type interfaceType, Type serviceType, ServiceRegistrar registrar)
        {
            ConstructorInfo[] serviceConstructors = serviceType.GetConstructors();
            foreach (var serviceConstructor in serviceConstructors)
            {
                var attribute = serviceConstructor.GetCustomAttribute<ServiceInitAttribute>();
                if (attribute != null)
                {
                    /* Get IgnoreAttribute for the constructor, find the constructor parameters 
                      with the specified names, if parameter name is matched  then set NULL to the parameter. */
                    var ignoreAttribute = serviceConstructor.GetCustomAttribute<IgnoreAttribute>();

                    /*Get parameters of service's constructor and inject corresponding repositories values to it */
                    ParameterInfo[] constructorParametersInfo = serviceConstructor.GetParameters();
                    Expression[] arguments = new Expression[constructorParametersInfo.Length];

                    int index = 0;
                    foreach (var constrParameter in constructorParametersInfo)
                    {

                        bool ignore = ignoreAttribute?.ParameterNames.Contains(constrParameter.Name)??false;

                        if (ignore)
                        {
                            arguments[index] = Expression.Default(constrParameter.ParameterType);
                        }
                        else
                        {
                            //let's subcontract take responsibility to create dependency objects 
                            arguments[index] = DependencyFactory?.Create(constrParameter.ParameterType, registrar) ?? Expression.Default(constrParameter.ParameterType);
                        }
                        index++;
                    }
                    return Expression.New(serviceConstructor, arguments);
                }
            }

            //Default constructor
            return Expression.New(serviceType);
        }
    }
}
