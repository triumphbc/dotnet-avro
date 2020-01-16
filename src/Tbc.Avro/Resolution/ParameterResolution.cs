using System;
using System.Reflection;

namespace Tbc.Avro.Resolution
{
    /// <summary>
    /// Contains resolved information about a constructor or method parameter.
    /// </summary>
    public class ParameterResolution
    {
        private ParameterInfo parameter = null!;

        private IdentifierResolution name = null!;

        private Type type = null!;

        /// <summary>
        /// The resolved parameter reflection info.
        /// </summary>
        public virtual ParameterInfo Parameter
        {
            get
            {
                return parameter ?? throw new InvalidOperationException();
            }
            set
            {
                parameter = value ?? throw new ArgumentNullException(nameof(value), "Parameter reflection info cannot be null.");
            }
        }

        /// <summary>
        /// The parameter name.
        /// </summary>
        public virtual IdentifierResolution Name
        {
            get
            {
                return name ?? throw new InvalidOperationException();
            }
            set
            {
                name = value ?? throw new ArgumentNullException(nameof(value), "Parameter name cannot be null.");
            }
        }

        /// <summary>
        /// The parameter type.
        /// </summary>
        public virtual Type Type
        {
            get
            {
                return type ?? throw new InvalidOperationException();
            }
            set
            {
                type = value ?? throw new ArgumentNullException(nameof(value), "Parameter type cannot be null.");
            }
        }

        /// <summary>
        /// Creates a new parameter resolution.
        /// </summary>
        /// <param name="parameter">
        /// The resolved parameter reflection info.
        /// </param>
        /// <param name="type">
        /// The parameter type.
        /// </param>
        /// <param name="name">
        /// The parameter name.
        /// </param>
        public ParameterResolution(ParameterInfo parameter, Type type, IdentifierResolution name)
        {
            Parameter = parameter;
            Name = name;
            Type = type;
        }
    }
}
