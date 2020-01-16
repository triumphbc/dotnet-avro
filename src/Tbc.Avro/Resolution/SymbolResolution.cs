using System;
using System.Reflection;

namespace Tbc.Avro.Resolution
{
    /// <summary>
    /// Contains resolved information about an enum symbol.
    /// </summary>
    public class SymbolResolution
    {
        private MemberInfo member = null!;

        private IdentifierResolution name = null!;

        private object value = null!;

        /// <summary>
        /// The resolved static field reflection info.
        /// </summary>
        public virtual MemberInfo Member
        {
            get
            {
                return member ?? throw new InvalidOperationException();
            }
            set
            {
                member = value ?? throw new ArgumentNullException(nameof(value), "Symbol reflection info cannot be null.");
            }
        }

        /// <summary>
        /// The symbol name.
        /// </summary>
        public virtual IdentifierResolution Name
        {
            get
            {
                return name ?? throw new InvalidOperationException();
            }
            set
            {
                name = value ?? throw new ArgumentNullException(nameof(value), "Symbol name cannot be null.");
            }
        }

        /// <summary>
        /// The raw symbol value.
        /// </summary>
        public virtual object Value
        {
            get
            {
                return value ?? throw new InvalidOperationException();
            }
            set
            {
                this.value = value ?? throw new ArgumentNullException(nameof(value), "Symbol value cannot be null.");
            }
        }

        /// <summary>
        /// Creates a new symbol resolution.
        /// </summary>
        /// <param name="member">
        /// The resolved static field reflection info.
        /// </param>
        /// <param name="name">
        /// The symbol name.
        /// </param>
        /// <param name="value">
        /// The raw symbol value.
        /// </param>
        public SymbolResolution(MemberInfo member, IdentifierResolution name, object value)
        {
            Member = member;
            Name = name;
            Value = value;
        }
    }
}
