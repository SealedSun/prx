/*
 * Prexonite, a scripting engine (Scripting Language -> Bytecode -> Virtual Machine)
 *  Copyright (C) 2007  Christian "SealedSun" Klauser
 *  E-mail  sealedsun a.t gmail d.ot com
 *  Web     http://www.sealedsun.ch/
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2 of the License, or
 *  (at your option) any later version.
 *
 *  Please contact me (sealedsun a.t gmail do.t com) if you need a different license.
 * 
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License along
 *  with this program; if not, write to the Free Software Foundation, Inc.,
 *  51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
 */

using System;
using Prexonite.Compiler.Cil;

namespace Prexonite.Types
{
    [PTypeLiteral("Real")]
    public class RealPType : PType, ICilCompilerAware
    {
        #region Singleton pattern

        private static readonly RealPType instance;

        public static RealPType Instance
        {
            get { return instance; }
        }

        static RealPType()
        {
            instance = new RealPType();
        }

        private RealPType()
        {
        }

        #endregion

        #region Static

        public PValue CreatePValue(double value)
        {
            return new PValue(value, Instance);
        }

        public PValue CreatePValue(float value)
        {
            return new PValue((double)value, Instance);
        }

        #endregion

        #region Access interface implementation

        public override bool TryContruct(StackContext sctx, PValue[] args, out PValue result)
        {
            if (args.Length <= 1)
            {
                result = Real.CreatePValue(0.0);
                return true;
            }
            else
                return args[0].TryConvertTo(sctx, Real, out result);
        }

        public override bool TryDynamicCall(
            StackContext sctx,
            PValue subject,
            PValue[] args,
            PCall call,
            string id,
            out PValue result)
        {
            Object[typeof(double)].TryDynamicCall(sctx, subject, args, call, id, out result);

            return result != null;
        }

        public override bool TryStaticCall(
            StackContext sctx, PValue[] args, PCall call, string id, out PValue result)
        {
            Object[typeof(double)].TryStaticCall(sctx, args, call, id, out result);

            return result != null;
        }

        protected override bool InternalConvertTo(
            StackContext sctx,
            PValue subject,
            PType target,
            bool useExplicit,
            out PValue result)
        {
            result = null;

            if (useExplicit)
            {
                if (target is ObjectPType)
                {
                    switch (Type.GetTypeCode(((ObjectPType) target).ClrType))
                    {
                        case TypeCode.Byte:
                            result = CreateObject((Byte) (Double) subject.Value);
                            break;
                        case TypeCode.SByte:
                            result = CreateObject((SByte) (Double) subject.Value);
                            break;
                        case TypeCode.Int32:
                            result = CreateObject((Int32) (Double) subject.Value);
                            break;
                        case TypeCode.UInt32:
                            result = CreateObject((UInt32) (Double) subject.Value);
                            break;
                        case TypeCode.Int16:
                            result = CreateObject((Int16) (Double) subject.Value);
                            break;
                        case TypeCode.UInt16:
                            result = CreateObject((UInt16) (Double) subject.Value);
                            break;
                        case TypeCode.Int64:
                            result = CreateObject((Int64) (Double) subject.Value);
                            break;
                        case TypeCode.UInt64:
                            result = CreateObject((UInt64) (Double) subject.Value);
                            break;
                        case TypeCode.Single:
                            result = CreateObject((Single) (Double) subject.Value);
                            break;
                    }
                }
            }

            // (!useImplicit)
            if (result == null)
            {
                if (target is StringPType)
                    result = String.CreatePValue(subject.Value.ToString());
                else if (target is RealPType)
                    result = Real.CreatePValue((double) subject.Value);
                else if (target is BoolPType)
                    result = Bool.CreatePValue(((double) subject.Value) != 0.0);
                else if (target is ObjectPType)
                {
                    switch (Type.GetTypeCode(((ObjectPType) target).ClrType))
                    {
                        case TypeCode.Double:
                            result = CreateObject((Double) subject.Value);
                            break;
                        case TypeCode.Decimal:
                            result = CreateObject((Decimal) subject.Value);
                            break;
                    }
                }
            }

            return result != null;
        }

        protected override bool InternalConvertFrom(
            StackContext sctx,
            PValue subject,
            bool useExplicit,
            out PValue result)
        {
            result = null;
            PType subjectType = subject.Type;
            if (subjectType is StringPType)
            {
                double value;
                if (double.TryParse(subject.Value as string, out value))
                    result = value; //Conversion succeeded
                else if (useExplicit)
                    return false; //Conversion required, provoke error
                else
                    result = 0; //Conversion not required, return default value
            }
            else if (subjectType is ObjectPType)
            {
                if (useExplicit)
                    switch (Type.GetTypeCode((subjectType as ObjectPType).ClrType))
                    {
                        case TypeCode.Decimal:
                        case TypeCode.Char:
                            result = (double) subject.Value;
                            break;
                        case TypeCode.Boolean:
                            result = ((bool) subject.Value) ? 1.0 : 0.0;
                            break;
                    }

                if (result != null)
                {
//(!useExplicit || useExplicit)
                    switch (Type.GetTypeCode((subjectType as ObjectPType).ClrType))
                    {
                        case TypeCode.Byte:
                        case TypeCode.SByte:
                        case TypeCode.Int16:
                        case TypeCode.UInt16:
                        case TypeCode.Int32:
                        case TypeCode.UInt32:
                        case TypeCode.Int64:
                        case TypeCode.UInt64:
                        case TypeCode.Single:
                        case TypeCode.Double:
                            result = (double) subject.Value;
                            break;
                    }
                }
            }

            return result != null;
        }

        private static bool _tryConvertToReal(StackContext sctx, PValue operand, out double value)
        {
            return _tryConvertToReal(sctx, operand, out value, true);
        }

        private static bool _tryConvertToReal(
            StackContext sctx, PValue operand, out double value, bool allowNull)
        {
            value = -133.7; //should never surface as value is only used if the method returns true

            switch (operand.Type.ToBuiltIn())
            {
                case BuiltIn.Int:
                case BuiltIn.Real:
                    value = Convert.ToDouble(operand.Value);
                    return true;
                    /* 
                case BuiltIn.String:
                    string rawRight = operand.Value as string;
                    if (!double.TryParse(rawRight, out value))
                        value = 0.0;
                    return true; //*/
                case BuiltIn.Object:
                    PValue pvRight;
                    if (operand.TryConvertTo(sctx, Real, out pvRight))
                    {
                        value = (int) pvRight.Value;
                        return true;
                    }
                    break;
                case BuiltIn.Null:
                    value = 0.0;
                    return allowNull;
                default:
                    break;
            }

            return false;
        }

        public override bool Addition(
            StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left) &&
                _tryConvertToReal(sctx, rightOperand, out right))
                result = left + right;

            return result != null;
        }

        public override bool Subtraction(
            StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left) &&
                _tryConvertToReal(sctx, rightOperand, out right))
                result = left - right;

            return result != null;
        }

        public override bool Multiply(
            StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left) &&
                _tryConvertToReal(sctx, rightOperand, out right))
                result = left*right;

            return result != null;
        }

        public override bool Division(
            StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left) &&
                _tryConvertToReal(sctx, rightOperand, out right))
                result = left/right;

            return result != null;
        }

        public override bool Modulus(
            StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left) &&
                _tryConvertToReal(sctx, rightOperand, out right))
                result = left%right;

            return result != null;
        }

        public override bool Equality(
            StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left, false) &&
                _tryConvertToReal(sctx, rightOperand, out right, false))
                result = left == right;

            return result != null;
        }

        public override bool Inequality(
            StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left, false) &&
                _tryConvertToReal(sctx, rightOperand, out right, false))
                result = left != right;

            return result != null;
        }

        public override bool GreaterThan(
            StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left) &&
                _tryConvertToReal(sctx, rightOperand, out right))
                result = left > right;

            return result != null;
        }

        public override bool GreaterThanOrEqual(
            StackContext sctx,
            PValue leftOperand,
            PValue rightOperand,
            out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left) &&
                _tryConvertToReal(sctx, rightOperand, out right))
                result = left >= right;

            return result != null;
        }

        public override bool LessThan(
            StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left) &&
                _tryConvertToReal(sctx, rightOperand, out right))
                result = left < right;

            return result != null;
        }

        public override bool LessThanOrEqual(
            StackContext sctx,
            PValue leftOperand,
            PValue rightOperand,
            out PValue result)
        {
            result = null;
            double left;
            double right;

            if (_tryConvertToReal(sctx, leftOperand, out left) &&
                _tryConvertToReal(sctx, rightOperand, out right))
                result = left <= right;

            return result != null;
        }

        public override bool UnaryNegation(StackContext sctx, PValue operand, out PValue result)
        {
            result = null;
            double op;
            if (_tryConvertToReal(sctx, operand, out op))
                result = -op;

            return result != null;
        }

        public override bool Increment(StackContext sctx, PValue operand, out PValue result)
        {
            result = null;
            double op;
            if (_tryConvertToReal(sctx, operand, out op))
                result = op + 1.0;

            return result != null;
        }

        public override bool Decrement(StackContext sctx, PValue operand, out PValue result)
        {
            result = null;
            double op;
            if (_tryConvertToReal(sctx, operand, out op))
                result = op - 1.0;

            return result != null;
        }

        protected override bool InternalIsEqual(PType otherType)
        {
            return otherType is RealPType;
        }

        private const int _code = -2035946599;

        public override int GetHashCode()
        {
            return _code;
        }

        #endregion

        public const string Literal = "Real";

        public override string ToString()
        {
            return Literal;
        }

        #region ICilCompilerAware Members

        /// <summary>
        /// Asses qualification and preferences for a certain instruction.
        /// </summary>
        /// <param name="ins">The instruction that is about to be compiled.</param>
        /// <returns>A set of <see cref="CompilationFlags"/>.</returns>
        CompilationFlags ICilCompilerAware.CheckQualification(Instruction ins)
        {
            return CompilationFlags.PreferCustomImplementation;
        }

        /// <summary>
        /// Provides a custom compiler routine for emitting CIL byte code for a specific instruction.
        /// </summary>
        /// <param name="state">The compiler state.</param>
        /// <param name="ins">The instruction to compile.</param>
        void ICilCompilerAware.ImplementInCil(CompilerState state, Instruction ins)
        {
            state.EmitCall(Compiler.Cil.Compiler.GetRealPType);
        }

        #endregion
    }
}