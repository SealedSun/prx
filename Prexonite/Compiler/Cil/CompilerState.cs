// /*
//  * Prexonite, a scripting engine (Scripting Language -> Bytecode -> Virtual Machine)
//  *  Copyright (C) 2007  Christian "SealedSun" Klauser
//  *  E-mail  sealedsun a.t gmail d.ot com
//  *  Web     http://www.sealedsun.ch/
//  *
//  *  This program is free software; you can redistribute it and/or modify
//  *  it under the terms of the GNU General Public License as published by
//  *  the Free Software Foundation; either version 2 of the License, or
//  *  (at your option) any later version.
//  *
//  *  Please contact me (sealedsun a.t gmail do.t com) if you need a different license.
//  * 
//  *  This program is distributed in the hope that it will be useful,
//  *  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  *  GNU General Public License for more details.
//  *
//  *  You should have received a copy of the GNU General Public License along
//  *  with this program; if not, write to the Free Software Foundation, Inc.,
//  *  51 Franklin Street, Fifth Floor, Boston, MA 02110-1301 USA.
//  */

#region Namespace Imports

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Prexonite.Commands;
using Prexonite.Types;

#endregion

namespace Prexonite.Compiler.Cil
{
    public sealed class CompilerState : StackContext
    {
        public const int ParamArgsIndex = 2;
        public const int ParamResultIndex = 4;
        public const int ParamSctxIndex = 1;
        public const int ParamSharedVariablesIndex = 3;
        public const int ParamSourceIndex = 0;
        public const int ParamReturnModeIndex = 5;
        private readonly List<ForeachHint> _foreachHints;
        private readonly Queue<int> _cilExtensionOffsets;
        private readonly ILGenerator _il;
        private readonly Dictionary<int, string> _indexMap;
        private readonly Label[] _instructionLabels;
        private readonly Label _returnLabel;
        private readonly PFunction _source;
        private readonly SymbolTable<Symbol> _symbols;
        private readonly Engine _targetEngine;
        private readonly Stack<TryCatchFinallyBlock> _tryBlocks;
        private LocalBuilder[] _tempLocals;
        private readonly CompilerPass _pass;
        private readonly FunctionLinking _linking;

        public CompilerState
            (PFunction source, Engine targetEngine, ILGenerator il, CompilerPass pass, FunctionLinking linking)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (targetEngine == null)
                throw new ArgumentNullException("targetEngine");
            if (il == null)
                throw new ArgumentNullException("il");

            _source = source;
            _linking = linking;
            _pass = pass;
            _targetEngine = targetEngine;
            _il = il;
            _indexMap = new Dictionary<int, string>();
            _instructionLabels = new Label[Source.Code.Count + 1];
            for (var i = 0; i < InstructionLabels.Length; i++)
                InstructionLabels[i] = il.DefineLabel();
            _returnLabel = il.DefineLabel();
            _symbols = new SymbolTable<Symbol>();
            _tryBlocks = new Stack<TryCatchFinallyBlock>();

            MetaEntry cilHints;
            _foreachHints = new List<ForeachHint>();
            _cilExtensionOffsets = new Queue<int>();
            if (source.Meta.TryGetValue(Loader.CilHintsKey, out cilHints))
            {
                SortedSet<int> cilExtensionOffsets = null;
                foreach (var entry in cilHints.List)
                {
                    var hint = entry.List;
                    if (hint.Length < 1)
                        continue;
                    switch (hint[0].Text)
                    {
                        case CilExtensionHint.Key:
                            if(cilExtensionOffsets == null)
                                cilExtensionOffsets = new SortedSet<int>();
                            var cilExt = CilExtensionHint.FromMetaEntry(hint);
                            foreach (var offset in cilExt.Offsets)
                                cilExtensionOffsets.Add(offset);
                            break;
                        case ForeachHint.Key:
                            ForeachHints.Add(ForeachHint.FromMetaEntry(hint));
                            break;
                    }
                }
                if(cilExtensionOffsets != null)
                {
                    foreach (var offset in cilExtensionOffsets)
                        _cilExtensionOffsets.Enqueue(offset);
                }
            }
        }

        #region Accessors

        public LocalBuilder ArgcLocal { get; internal set; }

        public PFunction Source
        {
            get { return _source; }
        }

        public LocalBuilder ArgvLocal { get; internal set; }

        public ILGenerator Il
        {
            get { return _il; }
        }

        public Dictionary<int, string> IndexMap
        {
            get { return _indexMap; }
        }

        public Label[] InstructionLabels
        {
            get { return _instructionLabels; }
        }

        public Label ReturnLabel
        {
            get { return _returnLabel; }
        }

        public LocalBuilder SctxLocal { get; internal set; }

        public LocalBuilder SharedLocal { get; internal set; }

        public SymbolTable<Symbol> Symbols
        {
            get { return _symbols; }
        }

        public LocalBuilder[] TempLocals
        {
            get { return _tempLocals; }
            internal set { _tempLocals = value; }
        }

        public Stack<TryCatchFinallyBlock> TryBlocks
        {
            get { return _tryBlocks; }
        }

        public Engine TargetEngine
        {
            get { return _targetEngine; }
        }

        public List<ForeachHint> ForeachHints
        {
            get { return _foreachHints; }
        }

        public Queue<int> CilExtensionOffsets
        {
            [DebuggerStepThrough]
            get { return _cilExtensionOffsets; }
        }

        /// <summary>
        /// Represents the engine this context is part of.
        /// </summary>
        public override Engine ParentEngine
        {
            get { return _targetEngine; }
        }

        /// <summary>
        /// The parent application.
        /// </summary>
        public override Application ParentApplication
        {
            get { return _source.ParentApplication; }
        }

        public override SymbolCollection ImportedNamespaces
        {
            get { return _source.ImportedNamespaces; }
        }

        /// <summary>
        /// Indicates whether the context still has code/work to do.
        /// </summary>
        /// <returns>True if the context has additional work to perform in the next cycle, False if it has finished it's work and can be removed from the stack</returns>
        protected override bool PerformNextCylce(StackContext lastContext)
        {
            return false;
        }

        /// <summary>
        /// Tries to handle the supplied exception.
        /// </summary>
        /// <param name="exc">The exception to be handled.</param>
        /// <returns>True if the exception has been handled, false otherwise.</returns>
        public override bool TryHandleException(Exception exc)
        {
            return false;
        }

        /// <summary>
        /// Represents the return value of the context.
        /// Just providing a value here does not mean that it gets consumed by the caller.
        /// If the context does not provide a return value, this property should return null (not NullPType).
        /// </summary>
        public override PValue ReturnValue
        {
            get { return PType.Null; }
        }

        /// <summary>
        /// Returns a reference to the current compiler pass.
        /// </summary>
        public CompilerPass Pass
        {
            get { return _pass; }
        }

        public FunctionLinking Linking
        {
            get { return _linking; }
        }

        #endregion

        #region Emit-helper methods

        public void EmitLdcI4(int i)
        {
            switch (i)
            {
                case -1:
                    Il.Emit(OpCodes.Ldc_I4_M1);
                    break;
                case 0:
                    Il.Emit(OpCodes.Ldc_I4_0);
                    break;
                case 1:
                    Il.Emit(OpCodes.Ldc_I4_1);
                    break;
                case 2:
                    Il.Emit(OpCodes.Ldc_I4_2);
                    break;
                case 3:
                    Il.Emit(OpCodes.Ldc_I4_3);
                    break;
                case 4:
                    Il.Emit(OpCodes.Ldc_I4_4);
                    break;
                case 5:
                    Il.Emit(OpCodes.Ldc_I4_5);
                    break;
                case 6:
                    Il.Emit(OpCodes.Ldc_I4_6);
                    break;
                case 7:
                    Il.Emit(OpCodes.Ldc_I4_7);
                    break;
                case 8:
                    Il.Emit(OpCodes.Ldc_I4_8);
                    break;
                default:
                    if (i >= SByte.MinValue && i <= SByte.MaxValue)
                        Il.Emit(OpCodes.Ldc_I4_S, (sbyte) i);
                    else
                        Il.Emit(OpCodes.Ldc_I4, i);
                    break;
            }
        }

        //CompilerState state, 
        public bool MustUseLeave(int instructionAddress, ref int targetAddress)
        {
            var useLeave = false;
            foreach (var enclosingBlock in TryBlocks)
            {
                if (enclosingBlock.Handles(instructionAddress))
                {
                    if (!enclosingBlock.Handles(targetAddress))
                    {
                        useLeave = true;
                        //To skip a try block in Prexonite, one jumps to the first finally instruction.
                        // This is illegal in CIL. The same behaviour is achieved by leaving the try block as
                        //  the finally clause is automatically executed first.
                        // As for try-catch: The Prexonite "leave" instruction has no representation in CIL and can therefore not
                        //  be targeted directly. The same workaround applies.
                        if (targetAddress == enclosingBlock.BeginFinally ||
                            targetAddress == enclosingBlock.BeginCatch)
                            targetAddress = enclosingBlock.EndTry;
                        break;
                    }
                    else
                    {
                        //remains a local jump so far
                    }
                }
                else
                {
                    if (enclosingBlock.Handles(targetAddress))
                        throw new PrexoniteException("Jumps into guarded (try) blocks are illegal.");
                    //remains an external jump so far
                }
            }
            return useLeave;
        }

        /// <summary>
        /// Shove arguments from the stack into the argument array (`argv`). This way the arguments can later be
        /// passed to methods. Use <see cref="ReadArgv"/>(<paramref name="argc"/>) to load that array onto the stack.
        /// </summary>
        /// <param name="argc">The number of arguments to load from the stack.</param>
        public void FillArgv(int argc)
        {
            if (argc == 0)
            {
                //Nothing, argv is read from CilRuntime.EmptyPValueArrayField
            }
            else
            {
                //Instantiate array -> argv
                Il.Emit(OpCodes.Ldc_I4, argc);
                Il.Emit(OpCodes.Newarr, typeof (PValue));
                EmitStoreLocal(ArgvLocal);

                for (var i = argc - 1; i >= 0; i--)
                {
                    //get argument
                    EmitStoreTemp(0);

                    //store argument
                    EmitLoadLocal(ArgvLocal);
                    EmitLdcI4(i);
                    EmitLoadTemp(0);
                    Il.Emit(OpCodes.Stelem_Ref);
                }
            }
        }

        /// <summary>
        /// Load previously perpared argument array (<see cref="FillArgv"/>) onto the stack. 
        /// </summary>
        /// <param name="argc">The number of elements in that argument array.</param>
        public void ReadArgv(int argc)
        {
            if (argc == 0)
            {
                Il.Emit(OpCodes.Ldsfld, Runtime.EmptyPValueArrayField);
            }
            else
            {
                EmitLoadLocal(ArgvLocal);
            }
        }

        public void EmitLoadArg(int index)
        {
            switch (index)
            {
                case 0:
                    Il.Emit(OpCodes.Ldarg_0);
                    break;
                case 1:
                    Il.Emit(OpCodes.Ldarg_1);
                    break;
                case 2:
                    Il.Emit(OpCodes.Ldarg_2);
                    break;
                case 3:
                    Il.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    if (index < Byte.MaxValue)
                        Il.Emit(OpCodes.Ldarg_S, (byte) index);
                    else
                        Il.Emit(OpCodes.Ldarg, index);
                    break;
            }
        }

        public void EmitLoadLocal(LocalBuilder local)
        {
            EmitLoadLocal(local.LocalIndex);
        }

        public void EmitLoadLocal(int index)
        {
            switch (index)
            {
                case 0:
                    Il.Emit(OpCodes.Ldloc_0);
                    break;
                case 1:
                    Il.Emit(OpCodes.Ldloc_1);
                    break;
                case 2:
                    Il.Emit(OpCodes.Ldloc_2);
                    break;
                case 3:
                    Il.Emit(OpCodes.Ldloc_3);
                    break;
                default:
                    if (index < Byte.MaxValue)
                        Il.Emit(OpCodes.Ldloc_S, (byte) index);
                    else
                        Il.Emit(OpCodes.Ldloc, index);
                    break;
            }
        }

        public void EmitStoreLocal(LocalBuilder local)
        {
            EmitStoreLocal(local.LocalIndex);
        }

        public void EmitStoreLocal(int index)
        {
            switch (index)
            {
                case 0:
                    Il.Emit(OpCodes.Stloc_0);
                    break;
                case 1:
                    Il.Emit(OpCodes.Stloc_1);
                    break;
                case 2:
                    Il.Emit(OpCodes.Stloc_2);
                    break;
                case 3:
                    Il.Emit(OpCodes.Stloc_3);
                    break;
                default:
                    if (index < Byte.MaxValue)
                        Il.Emit(OpCodes.Stloc_S, (byte) index);
                    else
                        Il.Emit(OpCodes.Stloc, index);
                    break;
            }
        }

        public void EmitStoreTemp(int i)
        {
            if (i >= _tempLocals.Length)
                throw new ArgumentOutOfRangeException
                    (
                    "i", i, "This particular cil implementation does not use that many temporary variables.");
            EmitStoreLocal(_tempLocals[i]);
        }

        public void EmitLoadTemp(int i)
        {
            if (i >= _tempLocals.Length)
                throw new ArgumentOutOfRangeException
                    (
                    "i", i, "This particular cil implementation does not use that many temporary variables.");
            EmitLoadLocal(_tempLocals[i]);
        }

        public void EmitLoadGlobalValue(string id)
        {
            EmitLoadLocal(SctxLocal.LocalIndex);
            Il.Emit(OpCodes.Ldstr, id);
            Il.EmitCall(OpCodes.Call, Runtime.LoadGlobalVariableReferenceMethod, null);
            Il.EmitCall(OpCodes.Call, Compiler.GetValueMethod, null);
        }

        public void EmitIndirectCall(int argc, bool justEffect)
        {
            EmitLoadLocal(SctxLocal);
            ReadArgv(argc);
            Il.EmitCall(OpCodes.Call, Compiler.PVIndirectCallMethod, null);
            if (justEffect)
                Il.Emit(OpCodes.Pop);
        }

        public void EmitStorePValue(Symbol sym, Action action)
        {
            if (sym.Kind == SymbolKind.Local)
            {
                action();
                EmitStoreLocal(sym.Local);
            }
            else if (sym.Kind == SymbolKind.LocalRef)
            {
                EmitLoadLocal(sym.Local);
                action();
                Il.EmitCall(OpCodes.Call, Compiler.SetValueMethod, null);
            }
            else
            {
                throw new PrexoniteException("Cannot emit code for Symbol");
            }
        }

        public void EmitLoadPValue(Symbol sym)
        {
            if (sym.Kind == SymbolKind.Local)
            {
                EmitLoadLocal(sym.Local);
            }
            else if (sym.Kind == SymbolKind.LocalRef)
            {
                EmitLoadLocal(sym.Local);
                Il.EmitCall(OpCodes.Call, Compiler.GetValueMethod, null);
            }
            else
            {
                throw new PrexoniteException("Cannot emit code for Symbol");
            }
        }

        public void EmitLoadPValueNull()
        {
            Il.EmitCall(OpCodes.Call, Compiler.getPTypeNull, null);
            Il.EmitCall(OpCodes.Call, Compiler.nullCreatePValue, null);
        }

        public void MarkInstruction(int instructionAddress)
        {
            Il.MarkLabel(InstructionLabels[instructionAddress]);
        }

        public void EmitCall(MethodInfo method)
        {
            Il.EmitCall(OpCodes.Call, method, null);
        }

        public void EmitVirtualCall(MethodInfo method)
        {
            Il.EmitCall(OpCodes.Callvirt, method, null);
        }

        public void EmitWrapString()
        {
            Il.EmitCall(OpCodes.Call, Compiler.GetStringPType, null);
            Il.Emit(OpCodes.Newobj, Compiler.NewPValue);
        }

        public void EmitWrapBool()
        {
            Il.Emit(OpCodes.Box, typeof (bool));
            Il.EmitCall(OpCodes.Call, Compiler.GetBoolPType, null);
            Il.Emit(OpCodes.Newobj, Compiler.NewPValue);
        }

        public void EmitWrapReal()
        {
            Il.Emit(OpCodes.Box, typeof (double));
            Il.EmitCall(OpCodes.Call, Compiler.GetRealPType, null);
            Il.Emit(OpCodes.Newobj, Compiler.NewPValue);
        }

        public void EmitWrapInt()
        {
            Il.Emit(OpCodes.Box, typeof (int));
            Il.EmitCall(OpCodes.Call, Compiler.GetIntPType, null);
            Il.Emit(OpCodes.Newobj, Compiler.NewPValue);
        }

        public void EmitWrapChar()
        {
            Il.Emit(OpCodes.Box, typeof(char));
            Il.EmitCall(OpCodes.Call, Compiler.GetCharPType, null);
            Il.Emit(OpCodes.Newobj, Compiler.NewPValue);
        }

        public void EmitLoadType(string typeExpr)
        {
            var T = ConstructPType(typeExpr);
            var cilT = T as ICilCompilerAware;

            CompilationFlags cf;
            var virtualInstruction = new Instruction(OpCode.cast_const, typeExpr);
            if (cilT != null)
            {
                cf = cilT.CheckQualification(virtualInstruction);
            }
            else
                cf = CompilationFlags.IsCompatible;

            if ((cf & CompilationFlags.HasCustomImplementation) == CompilationFlags.HasCustomImplementation &&
                cilT != null)
            {
                cilT.ImplementInCil(this, virtualInstruction);
            }
            else
            {
                EmitLoadLocal(SctxLocal);
                Il.Emit(OpCodes.Ldstr, typeExpr);
                EmitCall(Runtime.ConstructPTypeMethod);
            }
        }

        public void EmitNewObj(string typeExpr, int argc)
        {
            FillArgv(argc);
            EmitLoadType(typeExpr);
            EmitLoadLocal(SctxLocal);
            ReadArgv(argc);
            EmitVirtualCall(PType_ConstructMethod);
        }

        private static readonly MethodInfo PType_ConstructMethod =
            typeof (PType).GetMethod("Construct", new[] {typeof (StackContext), typeof (PValue[])});

        public void EmitLoadClrType(Type T)
        {
            Il.Emit(OpCodes.Ldtoken, T);
            EmitCall(Type_GetTypeFromHandle);
        }

        private static readonly MethodInfo Type_GetTypeFromHandle =
            typeof (Type).GetMethod("GetTypeFromHandle", new[] {typeof (RuntimeTypeHandle)});


        #region Early bound command call

        /// <summary>
        /// Emits a call to the static method "RunStatically(StackContext sctx, PValue[] args)" of the supplied type.
        /// </summary>
        /// <param name="target">The type, that declares the RunStatically to call.</param>
        /// <param name="ins">The call to the command for which code is to be emitted.</param>
        public void EmitEarlyBoundCommandCall(Type target, Instruction ins)
        {
            EmitEarlyBoundCommandCall(target, ins.Arguments, ins.JustEffect);
        }

        /// <summary>
        /// Emits a call to the static method "RunStatically(StackContext sctx, PValue[] args)" of the supplied type.
        /// </summary>
        /// <param name="target">The type, that declares the RunStatically to call.</param>
        /// <param name="argc">The number of arguments to pass to the command.</param>
        public void EmitEarlyBoundCommandCall(Type target, int argc)
        {
            EmitEarlyBoundCommandCall(target, argc, false);
        }

        /// <summary>
        /// Emits a call to the static method "RunStatically(StackContext sctx, PValue[] args)" of the supplied type.
        /// </summary>
        /// <param name="target">The type, that declares the RunStatically to call.</param>
        /// <param name="argc">The number of arguments to pass to the command.</param>
        /// <param name="justEffect">Indicates whether or not to ignore the return value.</param>
        public void EmitEarlyBoundCommandCall(Type target, int argc, bool justEffect)
        {
            var run =
                target.GetMethod("RunStatically", new[] {typeof (StackContext), typeof (PValue[])});

            if (run == null)
                throw new PrexoniteException
                    (
                    String.Format("{0} does not provide a static method RunStatically(StackContext, PValue[])", target));
            if (run.ReturnType != typeof (PValue))
                throw new PrexoniteException
                    (
                    String.Format("{0}'s RunStatically method does not return PValue but {1}.", target, run.ReturnType));
            FillArgv(argc);

            EmitLoadLocal(SctxLocal);
            ReadArgv(argc);
            Il.EmitCall(OpCodes.Call, run, null);
            if (justEffect)
                Il.Emit(OpCodes.Pop);
        }

        #endregion

        #endregion

        public void EmitIgnoreArguments(int argc)
        {
            for (var i = 0; i < argc; i++)
                Il.Emit(OpCodes.Pop);
        }

        public void MakePTypeFromExpr(string expr)
        {
            EmitLoadLocal(SctxLocal);
            Il.Emit(OpCodes.Ldstr, expr);
            Il.EmitCall(OpCodes.Call, Runtime.ConstructPTypeAsPValueMethod, null);
        }

        public bool TryGetStaticallyLinkedFunction(string id, out MethodInfo targetMethod)
        {
            targetMethod = null;
            return (Linking & FunctionLinking.Static) == FunctionLinking.Static &&
                   Pass.Implementations.TryGetValue(id, out targetMethod);
        }

        public bool TryCreateCompileTimeValue(ref CompileTimeValue compileTimeValue, Instruction ins)
        {
            var argc = ins.Arguments;
            var id = ins.Id;

            switch (ins.OpCode)
            {
                case OpCode.invalid:
                    break;
                case OpCode.nop:
                    break;
                case OpCode.ldc_int:
                    compileTimeValue.Interpretation = CompileTimeInterpretation.Int;
                    compileTimeValue.Value = ins.Arguments;
                    return true;
                case OpCode.ldc_real:
                    break;
                case OpCode.ldc_bool:
                    compileTimeValue.Interpretation = CompileTimeInterpretation.Bool;
                    compileTimeValue.Value = argc != 0;
                    return true;
                case OpCode.ldc_string:
                    compileTimeValue.Interpretation = CompileTimeInterpretation.String;
                    compileTimeValue.Value = id;
                    return true;
                case OpCode.ldc_null:
                    compileTimeValue.Interpretation = CompileTimeInterpretation.Null;
                    compileTimeValue.Value = null;
                    return true;
                case OpCode.ldr_loc:
                    compileTimeValue.Interpretation = CompileTimeInterpretation.LocalVariableReference;
                    compileTimeValue.Value = id;
                    return true;
                case OpCode.ldr_loci:
                    compileTimeValue.Interpretation = CompileTimeInterpretation.LocalVariableReference;
                    compileTimeValue.Value = IndexMap[argc];
                    return true;
                case OpCode.ldr_glob:
                    compileTimeValue.Interpretation = CompileTimeInterpretation.GlobalVariableReference;
                    compileTimeValue.Value = id;
                    return true;
                case OpCode.ldr_func:
                    compileTimeValue.Interpretation = CompileTimeInterpretation.FunctionReference;
                    compileTimeValue.Value = id;
                    return true;
                case OpCode.ldr_cmd:
                    compileTimeValue.Interpretation = CompileTimeInterpretation.CommandReference;
                    compileTimeValue.Value = id;
                    return true;
                case OpCode.ldr_app:
                    break;
                case OpCode.ldr_eng:
                    break;
                case OpCode.ldr_type:
                    break;
                case OpCode.ldloc:
                    break;
                case OpCode.stloc:
                    break;
                case OpCode.ldloci:
                    break;
                case OpCode.stloci:
                    break;
                case OpCode.ldglob:
                    break;
                case OpCode.stglob:
                    break;
                case OpCode.newobj:
                    break;
                case OpCode.newtype:
                    break;
                case OpCode.newclo:
                    break;
                case OpCode.newcor:
                    break;
                case OpCode.incloc:
                    break;
                case OpCode.incglob:
                    break;
                case OpCode.decloc:
                    break;
                case OpCode.decglob:
                    break;
                case OpCode.incloci:
                    break;
                case OpCode.decloci:
                    break;
                case OpCode.neg:
                    break;
                case OpCode.not:
                    break;
                case OpCode.add:
                    break;
                case OpCode.sub:
                    break;
                case OpCode.mul:
                    break;
                case OpCode.div:
                    break;
                case OpCode.mod:
                    break;
                case OpCode.pow:
                    break;
                case OpCode.ceq:
                    break;
                case OpCode.cne:
                    break;
                case OpCode.clt:
                    break;
                case OpCode.cle:
                    break;
                case OpCode.cgt:
                    break;
                case OpCode.cge:
                    break;
                case OpCode.or:
                    break;
                case OpCode.and:
                    break;
                case OpCode.xor:
                    break;
                case OpCode.check_const:
                    break;
                case OpCode.check_arg:
                    break;
                case OpCode.check_null:
                    break;
                case OpCode.cast_const:
                    break;
                case OpCode.cast_arg:
                    break;
                case OpCode.get:
                    break;
                case OpCode.set:
                    break;
                case OpCode.sget:
                    break;
                case OpCode.sset:
                    break;
                case OpCode.func:
                    break;
                case OpCode.cmd:
                    break;
                case OpCode.indarg:
                    break;
                case OpCode.tail:
                    break;
                case OpCode.indloc:
                    break;
                case OpCode.indloci:
                    break;
                case OpCode.indglob:
                    break;
                case OpCode.jump:
                    break;
                case OpCode.jump_t:
                    break;
                case OpCode.jump_f:
                    break;
                case OpCode.ret_exit:
                    break;
                case OpCode.ret_value:
                    break;
                case OpCode.ret_break:
                    break;
                case OpCode.ret_continue:
                    break;
                case OpCode.ret_set:
                    break;
                case OpCode.@throw:
                    break;
                case OpCode.@try:
                    break;
                case OpCode.leave:
                    break;
                case OpCode.exc:
                    break;
                case OpCode.pop:
                    break;
                case OpCode.dup:
                    break;
                case OpCode.rot:
                    break;
            }
            return false;
        }
    }
}