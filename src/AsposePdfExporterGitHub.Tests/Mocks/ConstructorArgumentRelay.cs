using System;
using System.Reflection;
using AutoFixture.Kernel;

namespace Aspose.Cloud.Marketplace.App.Github.Pdf.Exporter.Tests.Mocks
{
    /// <summary>
    /// AutoFixture helper class
    /// Specify value for specific constructor parameter
    /// </summary>
    /// <typeparam name="TTarget">Owner constructor type</typeparam>
    /// <typeparam name="TValueType">parameter type</typeparam>
    public class ConstructorArgumentRelay<TTarget, TValueType> : ISpecimenBuilder
    {
        private readonly string _paramName;
        private readonly TValueType _value;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="paramName">parameter name</param>
        /// <param name="value">specific value</param>
        public ConstructorArgumentRelay(string paramName, TValueType value)
        {
            _paramName = paramName;
            _value = value;
        }

        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            ParameterInfo parameter = request as ParameterInfo;
            if (parameter == null)
                return new NoSpecimen();
            if (parameter.Member.DeclaringType != typeof(TTarget) ||
                parameter.Member.MemberType != MemberTypes.Constructor ||
                parameter.ParameterType != typeof(TValueType) ||
                parameter.Name != _paramName)
                return new NoSpecimen();
            return _value;
        }
    }
}
