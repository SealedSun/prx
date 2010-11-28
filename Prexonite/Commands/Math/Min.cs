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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Prexonite.Compiler.Cil;
using Prexonite.Types;

namespace Prexonite.Commands.Math
{
    public class Min : PCommand, ICilCompilerAware
    {
        #region Singleton

        private Min()
        {
        }

        private static readonly Min _instance = new Min();

        public static Min Instance
        {
            get { return _instance; }
        }

        #endregion

        /// <summary>
        /// A flag indicating whether the command acts like a pure function.
        /// </summary>
        /// <remarks>Pure commands can be applied at compile time.</remarks>
        public virtual bool IsPure
        {
            get { return true; }
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="sctx">The stack context in which to execut the command.</param>
        /// <param name="args">The arguments to be passed to the command.</param>
        /// <returns>The value returned by the command. Must not be null. (But possibly {null~Null})</returns>
        public static PValue RunStatically(StackContext sctx, PValue[] args)
        {
            if (sctx == null)
                throw new ArgumentNullException("sctx");
            if (args == null)
                throw new ArgumentNullException("args");

            if (args.Length < 2)
                throw new PrexoniteException("Min requires at least two arguments.");

            var arg0 = args[0];
            var arg1 = args[1];
            return RunStatically(arg0, arg1, sctx);
        }

        public static PValue RunStatically(PValue arg0, PValue arg1, StackContext sctx)
        {
            if (arg0.Type == PType.Int && arg1.Type == PType.Int)
            {
                var a = (int) arg0.Value;
                var b = (int) arg1.Value;

                return System.Math.Min(a, b);
            }
            else
            {
                var a = (double) arg0.ConvertTo(sctx, PType.Real, true).Value;
                var b = (double) arg1.ConvertTo(sctx, PType.Real, true).Value;

                return System.Math.Min(a, b);
            }
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="sctx">The stack context in which to execut the command.</param>
        /// <param name="args">The arguments to be passed to the command.</param>
        /// <returns>The value returned by the command. Must not be null. (But possibly {null~Null})</returns>
        public override PValue Run(StackContext sctx, PValue[] args)
        {
            return RunStatically(sctx, args);
        }

        #region ICilCompilerAware Members

        /// <summary>
        /// Asses qualification and preferences for a certain instruction.
        /// </summary>
        /// <param name="ins">The instruction that is about to be compiled.</param>
        /// <returns>A set of <see cref="CompilationFlags"/>.</returns>
        CompilationFlags ICilCompilerAware.CheckQualification(Instruction ins)
        {
            switch (ins.Arguments)
            {
                case 0:
                case 1:
                case 2:
                    return CompilationFlags.PrefersCustomImplementation;
                default:
                    return CompilationFlags.PrefersRunStatically;
            }
        }

        private static readonly MethodInfo RunStaticallyMethod =
            typeof (Min).GetMethod("RunStatically", new[] {typeof (PValue), typeof (PValue), typeof (StackContext)});

        /// <summary>
        /// Provides a custom compiler routine for emitting CIL byte code for a specific instruction.
        /// </summary>
        /// <param name="state">The compiler state.</param>
        /// <param name="ins">The instruction to compile.</param>
        void ICilCompilerAware.ImplementInCil(CompilerState state, Instruction ins)
        {
            if (ins.JustEffect)
            {
                for (var i = 0; i < ins.Arguments; i++)
                    state.Il.Emit(OpCodes.Pop);
            }
            else
            {
                switch (ins.Arguments)
                {
                    case 0:
                        state.EmitLoadNullAsPValue();
                        state.EmitLoadNullAsPValue();
                        break;
                    case 1:
                        state.EmitLoadNullAsPValue();
                        break;
                    case 2:
                        break;
                    default:
                        throw new NotSupportedException();
                }

                state.EmitLoadLocal(state.SctxLocal);
                state.EmitCall(RunStaticallyMethod);
            }
        }

        #endregion
    }
}