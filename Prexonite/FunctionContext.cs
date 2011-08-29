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
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Prexonite.Commands;
using Prexonite.Commands.Core;
using Prexonite.Types;
using NoDebug = System.Diagnostics.DebuggerNonUserCodeAttribute;

#endregion

namespace Prexonite
{
    public class FunctionContext : StackContext
    {
        #region Creation

        public FunctionContext
            (
            Engine parentEngine,
            PFunction implementation,
            PValue[] args,
            PVariable[] sharedVariables)
            : this(parentEngine, implementation, args, sharedVariables, false)
        {
        }

        internal FunctionContext
            (
            Engine parentEngine,
            PFunction implementation,
            PValue[] args,
            PVariable[] sharedVariables,
            bool suppressInitialization)
        {
            if (parentEngine == null)
                throw new ArgumentNullException("parentEngine");
            if (implementation == null)
                throw new ArgumentNullException("implementation");
            if (sharedVariables == null)
                sharedVariables = new PVariable[] {};
            if (args == null)
                args = new PValue[] {};

            if (!(suppressInitialization || implementation.ParentApplication._SuppressInitialization))
                implementation.ParentApplication.EnsureInitialization(parentEngine, implementation);

            _parentEngine = parentEngine;
            _implementation = implementation;
            _bindArguments(args);
            _createLocalVariables();
            ReturnMode = ReturnMode.Exit;
            if (_implementation.Meta.ContainsKey(PFunction.SharedNamesKey))
            {
                var sharedNames = _implementation.Meta[PFunction.SharedNamesKey].List;
                //Ensure enough shared variables have been passed
                if (sharedNames.Length > sharedVariables.Length)
                    throw new ArgumentException
                        (
                        "The function " + _implementation.Id + " requires " +
                        sharedNames.Length + " variables to be shared.");


                for (var i = 0; i < sharedNames.Length; i++)
                {
                    if (sharedVariables[i] == null)
                        throw new ArgumentNullException
                            (
                            "sharedVariables",
                            String.Format("The element at index {0} passed in sharedVariables is null for function {1}.", i, implementation));

                    if (_localVariables.ContainsKey(sharedNames[i]))
                        continue; //Arguments are redeclarations, that is not shared 
                    _localVariables.Add(sharedNames[i], sharedVariables[i]);
                }
            }

            //Populate fast variable access array (call by index)
            _localVariableArray = new PVariable[_localVariables.Count];
            foreach (var mapping in _implementation.LocalVariableMapping)
                _localVariableArray[mapping.Value] = _localVariables[mapping.Key];
        }

        public FunctionContext(StackContext sctx, PFunction implementation, PValue[] args)
            : this(sctx.ParentEngine, implementation, args)
        {
        }

        public FunctionContext(Engine parentEngine, PFunction implementation, PValue[] args)
            : this(parentEngine, implementation, args, null)
        {
        }

        public FunctionContext(Engine parentEngine, PFunction implementation)
            : this(parentEngine, implementation, null)
        {
        }

        private void _bindArguments(PValue[] args)
        {
            //Create args variable
            const string argVId = PFunction.ArgumentListId;

            if (_implementation.Variables.Contains(argVId))
            {
                var argsV = new PVariable
                {
                    Value = _parentEngine.CreateNativePValue(args)
                };
                _localVariables.Add(argVId, argsV);
            }

            //Create actual parameter variables
            var i = 0;
            foreach (var arg in _implementation.Parameters)
            {
                var pvar = new PVariable();
                if (i < args.Length)
                    pvar.Value = args[i++];
                //Ensure it is a PValue
                if (pvar.Value == null)
                    pvar.Value = PType.Null.CreatePValue();

                _localVariables.Add(arg, pvar);
            }
        }

        private void _createLocalVariables()
        {
            //Create local variables
            foreach (var local in _implementation.Variables)
                if (!_localVariables.ContainsKey(local)) //Don't override arguments
                    _localVariables.Add(local, new PVariable());
        }

        #endregion

        #region Interface

        private PValue _returnValue;

        public override PValue ReturnValue
        {
            [DebuggerStepThrough]
            get { return _returnValue ?? NullPType.CreateValue(); }
            //Returns PValue(null) instead of just null.
        }

        private readonly Engine _parentEngine;

        public override Engine ParentEngine
        {
            [DebuggerStepThrough]
            get { return _parentEngine; }
        }

        private readonly PFunction _implementation;

        public PFunction Implementation
        {
            [DebuggerStepThrough]
            get { return _implementation; }
        }

        public override Application ParentApplication
        {
            get { return _implementation.ParentApplication; }
        }

        public override SymbolCollection ImportedNamespaces
        {
            get { return _implementation.ImportedNamespaces; }
        }

        public override string ToString()
        {
            return "context of " + _implementation;
        }

        #endregion

        #region Local variables

        private readonly SymbolTable<PVariable> _localVariables = new SymbolTable<PVariable>();
        private readonly PVariable[] _localVariableArray;

        public SymbolTable<PVariable> LocalVariables
        {
            [DebuggerStepThrough]
            get { return _localVariables; }
        }

        public void ReplaceLocalVariable(string name, PVariable newVariable)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("name");
            if (newVariable == null)
                throw new ArgumentNullException("newVariable");

            if (_implementation.LocalVariableMapping.ContainsKey(name))
                _localVariableArray[_implementation.LocalVariableMapping[name]] = newVariable;
            _localVariables[name] = newVariable;
        }

        #endregion

        #region Virtual Machine

        private int _pointer;

        public int Pointer
        {
            [DebuggerStepThrough]
            get { return _pointer; }
            [DebuggerStepThrough]
            set { _pointer = value; }
        }

        private readonly Stack<PValue> _stack = new Stack<PValue>();

        [DebuggerStepThrough]
        public void Push(PValue val)
        {
            if (_useVirtualStackInstead)
                _useVirtualStackInstead = false;
            else
                _stack.Push(val ?? NullPType.CreateValue());
        }

        [DebuggerStepThrough]
        public PValue Pop()
        {
            return _stack.Pop() ?? NullPType.CreateValue();
        }

        [DebuggerStepThrough]
        public PValue Peek()
        {
            return _stack.Peek() ?? NullPType.CreateValue();
        }

        public int StackSize
        {
            [DebuggerStepThrough]
            get { return _stack.Count; }
        }

        private void _throwInvalidStackException(int argc)
        {
            throw new PrexoniteInvalidStackException
                (
                "Code expects " + argc +
                " values on the stack but finds " + _stack.Count);
        }

        [DebuggerNonUserCode]
        private void _fillArgs(int argc, out PValue[] argv)
        {
            argv = new PValue[argc];
            if (_stack.Count < argc)
                _throwInvalidStackException(argc);
            for (var i = argc - 1; i >= 0; i--)
                argv[i] = Pop();
        }

        private bool _fetchReturnValue;

        protected override bool PerformNextCycle(StackContext lastContext)
        {
            return _performNextCylce(lastContext, false);
        }

        /// <summary>
        /// Same as <see cref="PerformNextCycle"/> but guarantees to only execute a single instruction.
        /// </summary>
        /// <param name="lastContext">Stack context of a called function that just returned. Must be set if the last step called a function/pushed a new context onto the VM stack. Is ignored otherwise.</param>
        /// <returns></returns>
        public bool Step(StackContext lastContext)
        {
            return _performNextCylce(lastContext, true);
        }

        /// <summary>
        /// <see cref="_UseVirtualMachineStackInstead"/>
        /// </summary>
        private bool _useVirtualStackInstead;

        /// <summary>
        /// When the function context calls into managed code, that code can push itself onto the virtual machine stack and then use this 
        /// method to instruct the calling function context to use the result of the virtual machine stack successor instead. (The return value of the managed code is discarded)
        /// </summary>
        internal void _UseVirtualMachineStackInstead()
        {
            _useVirtualStackInstead = true;
            _fetchReturnValue = true;
        }

        /// <summary>
        /// Implementation of <see cref="PerformNextCycle"/>.
        /// </summary>
        /// <param name="lastContext">Stack context of a called function that just returned. Must be set if the last step called a function/pushed a new context onto the VM stack. Is ignored otherwise.</param>
        /// <param name="needToReturn">Indicates whether to return after executing one instruction, even if more instructions could be combined into the cycle.</param>
        /// <returns>True if the context is not done yet, i.e., is to be kept on the VM stack; False if it is done, has produced a return value and should be removed from the VM stack.</returns>
        private bool _performNextCylce(StackContext lastContext, bool needToReturn)
        {
            //Indicates whether or not control needs to be returned to the VM.
            //  as long as no operation is performed on the stack, 
            //  
            var codeBase = _implementation.Code;
            var codeLength = codeBase.Count;
            do
            {
                if (_pointer >= codeLength)
                {
                    ReturnMode = ReturnMode.Exit;
                    return false;
                }

                //Get return value
                if (_fetchReturnValue)
                {
                    if (lastContext == null)
                        throw new PrexoniteException("Root function tries to fetch a return value.");
                    Push(lastContext.ReturnValue ?? PType.Null.CreatePValue());
                    _fetchReturnValue = false;
                }

                var ins = codeBase[_pointer++];

#if Verbose
            Console.Write("/* " + (_pointer-1).ToString().PadLeft(4, '0') + " */ " + ins);
            PValue val;
#endif

                var argc = ins.Arguments;
                var justEffect = ins.JustEffect;
                PValue[] argv;
                var id = ins.Id;
                PValue left;
                PValue right;
                var t = ins.GenericArgument as PType;
                PVariable pvar;
                PFunction func;
                //used by static calls
                int idx;
                string methodId;
                MemberInfo member;

                #region OPCODE HANDLING

                switch (ins.OpCode)
                {
                        #region NOP

                        //NOP
                    case OpCode.nop:
                        //Do nothing
                        break;

                        #endregion

                        #region LOAD

                        #region LOAD CONSTANT

                        //LOAD CONSTANT
                    case OpCode.ldc_int:
                        Push(argc);
                        break;
                    case OpCode.ldc_real:
                        Push((double) ins.GenericArgument);
                        break;
                    case OpCode.ldc_bool:
                        Push(argc != 0);
                        break;
                    case OpCode.ldc_string:
                        Push(id);
                        break;
                    case OpCode.ldc_null:
                        Push(PType.Null.CreatePValue());
                        break;

                        #endregion LOAD CONSTANT

                        #region LOAD REFERENCE

                        //LOAD REFERENCE
                    case OpCode.ldr_loc:
                        if (_localVariables.ContainsKey(id))
                            Push(CreateNativePValue(_localVariables[id]));
                        else
                            throw new PrexoniteException
                                (
                                string.Format
                                    (
                                    "Cannot load reference to local variable {0} in function {1}.",
                                    id,
                                    _implementation.Id));
                        break;
                    case OpCode.ldr_loci:
                        Push(CreateNativePValue(_localVariableArray[argc]));
                        break;
                    case OpCode.ldr_glob:
                        if (ParentApplication.Variables.TryGetValue(id, out pvar))
                            Push(CreateNativePValue(pvar));
                        else
                            throw new PrexoniteException
                                (
                                string.Format
                                    (
                                    "Cannot load reference to global variable {0} in application {1}.",
                                    id,
                                    ParentApplication.Id));
                        break;
                    case OpCode.ldr_func:
                        if (ParentApplication.Functions.Contains(id))
                            Push(CreateNativePValue(ParentApplication.Functions[id]));
                        else
                            throw new PrexoniteException
                                (
                                string.Format
                                    (
                                    "Cannot load reference to function {0} in application {1}.",
                                    id,
                                    ParentApplication.Id));
                        break;
                    case OpCode.ldr_cmd:
                        if (ParentEngine.Commands.Contains(id))
                            Push(CreateNativePValue(ParentEngine.Commands[id]));
                        else
                            throw new PrexoniteException
                                (
                                string.Format
                                    (
                                    "Cannot load reference to command {0}.",
                                    id));
                        break;
                    case OpCode.ldr_app:
                        Push(CreateNativePValue(ParentApplication));
                        break;
                    case OpCode.ldr_eng:
                        Push(CreateNativePValue(ParentEngine));
                        break;
                    case OpCode.ldr_type:
                        if ((object) t == null)
                        {
                            t = ConstructPType(id);
                            ins.GenericArgument = t;
                        }
                        Push(CreateNativePValue(t));
                        break;

                        #endregion //LOAD REFERENCE

                        #endregion //LOAD

                        #region VARIABLES

                        #region LOCAL

                        //LOAD LOCAL VARIABLE
                    case OpCode.ldloc:
                        pvar = _localVariables[id];
                        if (pvar == null)
                            throw new PrexoniteException
                                (
                                "The local variable " + id + " in function " + _implementation.Id +
                                " does not exist.");
#if Verbose
                    val = pvar.Value;
                    Console.Write("=" + _toDebug(val));
                    Push(val);
#else
                        Push(pvar.Value);
#endif
                        break;
                    case OpCode.stloc:
                        pvar = _localVariables[id];
                        if (pvar == null)
                            throw new PrexoniteException
                                (
                                "The local variable " + id + " in function " + _implementation.Id +
                                " does not exist.");

                        pvar.Value = Pop();
                        break;

                    case OpCode.ldloci:
                        Push(_localVariableArray[argc].Value);
                        break;

                    case OpCode.stloci:
                        _localVariableArray[argc].Value = Pop();
                        break;

                        #endregion

                        #region GLOBAL

                        //LOAD GLOBAL VARIABLE
                    case OpCode.ldglob:
                        var app = ParentApplication;
                        
                        if (!app.Variables.TryGetValue(id,out pvar))
                            throw _globalVariableDoesNotExistException(id);
                        app.EnsureInitialization(ParentEngine, pvar);
#if Verbose
                    val = pvar.Value;
                    Console.Write("=" + _toDebug(val));
                    Push(val);
#else
                        Push(pvar.Value);
#endif
                        break;
                    case OpCode.stglob:
                        if (!ParentApplication.Variables.TryGetValue(id, out pvar))
                            throw _globalVariableDoesNotExistException(id);
                        pvar.Value = Pop();
                        break;

                        #endregion

                        #endregion

                        #region CONSTRUCTION

                        //CONSTRUCTION
                    case OpCode.newobj:
                        if ((object) t == null)
                        {
                            t = ConstructPType(id);
                            ins.GenericArgument = t;
                        }
                        _fillArgs(argc, out argv);
                        Push(t.Construct(this, argv));
                        break;
                    case OpCode.newtype:
                        //assemble type expression
                        _fillArgs(argc, out argv);
                        Push(CreateNativePValue(ParentEngine.CreatePType(this, id, argv)));
                        break;

                    case OpCode.newclo:
                        var vars = ins.GenericArgument as string[];
                        func = ParentApplication.Functions[id];
                        if (vars == null)
                        {
                            MetaEntry[] entries;
                            if (func.Meta.ContainsKey(PFunction.SharedNamesKey))
                                entries = func.Meta[PFunction.SharedNamesKey].List;
                            else
                                entries = new MetaEntry[] {};
                            vars = new string[entries.Length];
                            for (var i = 0; i < entries.Length; i++)
                                vars[i] = entries[i].Text;
                            ins.GenericArgument = vars;
                        }
                        var pvars = new PVariable[vars.Length];
                        for (var i = 0; i < pvars.Length; i++)
                            pvars[i] = _localVariables[vars[i]];
                        if (func.HasCilImplementation)
                        {
                            Push(CreateNativePValue(new CilClosure(func, pvars)));
                        }
                        else
                        {
                            Push(CreateNativePValue(new Closure(func, pvars)));
                        }
                        break;

                    case OpCode.newcor:
                        _fillArgs(argc, out argv);

                        var routine = Pop();
                        var routineobj = routine.Value;
                        var routinesa = routineobj as IStackAware;
                        if (routineobj == null)
                        {
                            Push(PType.Null.CreatePValue());
                        }
                        else
                        {
                            StackContext corctx;
                            if (routinesa != null)
                                corctx = routinesa.CreateStackContext(this, argv);
                            else
                                corctx = (StackContext)
                                         routine.DynamicCall
                                             (
                                             this,
                                             new[]
                                             {
                                                 PType.Object.CreatePValue(ParentEngine),
                                                 PType.Object.CreatePValue(argv)
                                             },
                                             PCall.Get,
                                             "CreateStackContext").Value;

                            Push
                                (
                                PType.Object[typeof (Coroutine)].CreatePValue(new Coroutine(corctx)));
                        }
                        break;

                        #endregion

                        #region OPERATORS

                        #region UNARY

                        //UNARY OPERATORS
                    case OpCode.incloc:
                        pvar = _localVariables[id];
                        doIncrement:
                        pvar.Value = pvar.Value.Increment(this);
#if Verbose
                    Console.Write("=" + _toDebug(pvar.Value));
#endif
                        break;

                    case OpCode.incloci:
                        pvar = _localVariableArray[argc];
                        goto doIncrement;

                    case OpCode.incglob:
                        if (!ParentApplication.Variables.TryGetValue(id, out pvar))
                            throw _globalVariableDoesNotExistException(id); 
                        pvar.Value = pvar.Value.Increment(this);
#if Verbose
                    Console.Write("=" + _toDebug(pvar.Value));
#endif
                        break;
                    case OpCode.decloc:
                        pvar = _localVariables[id];
                        doDecrement:
                        pvar.Value = pvar.Value.Decrement(this);
#if Verbose
                    Console.Write("=" + _toDebug(pvar.Value));
#endif
                        break;
                    case OpCode.decloci:
                        pvar = _localVariableArray[argc];
                        goto doDecrement;

                    case OpCode.decglob:
                        if (!ParentApplication.Variables.TryGetValue(id, out pvar))
                            throw _globalVariableDoesNotExistException(id); 
                        pvar.Value = pvar.Value.Decrement(this);
#if Verbose
                    Console.Write("=" + _toDebug(pvar.Value));
#endif
                        break;

                        #endregion

                        #region BINARY

                        //binary operators are all implemented as commands in the namespace
                        //  Prexonite.Commands.Core.Operators

                        #endregion //OPERATORS

                        #endregion

                        #region TYPE OPERATIONS

                        #region TYPE CHECK

                        //TYPE CHECK
                    case OpCode.check_const:
                        if ((object) t == null)
                        {
                            t = ConstructPType(id);
                            ins.GenericArgument = t;
                        }
                        goto check_type; //common code
                    case OpCode.check_arg:
                        t = (PType) Pop().Value;
                        check_type:
                        Push(Pop().Type.Equals(t));
                        break;
                    case OpCode.check_null:
                        Push(Pop().IsNull);
                        break;

                        #endregion

                        #region TYPE CAST

                    case OpCode.cast_const:
                        if ((object) t == null)
                        {
                            t = ConstructPType(id);
                            ins.GenericArgument = t;
                        }
                        goto cast_type; //common code
                    case OpCode.cast_arg:
                        t = (PType) Pop().Value;
                        cast_type:
                        Push(Pop().ConvertTo(this, t, true));
                        break;

                        #endregion

                        #endregion

                        #region OBJECT CALLS

                        #region DYNAMIC

                    case OpCode.get:
                        _fillArgs(argc, out argv);
                        left = Pop();
                        right = left.DynamicCall(this, argv, PCall.Get, id);
                        if (!justEffect)
                            Push(right);
                        needToReturn = true;
                        break;
                    case OpCode.set:
                        _fillArgs(argc, out argv);
                        left = Pop();
                        left.DynamicCall(this, argv, PCall.Set, id);
                        needToReturn = true;
                        break;

                        #endregion

                        #region STATIC

                    case OpCode.sget:
                        _fillArgs(argc, out argv);
                        idx = id.LastIndexOf("::");
                        if (idx < 0)
                            throw new PrexoniteException
                                (
                                "Invalid sget instruction. Does not specify a method.");
                        needToReturn = true;
                        methodId = id.Substring(idx + 2);
                        member = ins.GenericArgument as MemberInfo;
                        if (member != null)
                            goto callByMemberGet;
                        else if ((object) t != null)
                            goto callByTypeGet;
                        else
                        {
                            //Full resolve
                            var typeExpr = id.Substring(0, idx);
                            t = ConstructPType(typeExpr);
                            ins.GenericArgument = t;
                            if (t is ObjectPType)
                            {
                                //Try to get a member info
                                var objT = (ObjectPType) t;
                                right = objT.StaticCall(this, argv, PCall.Get, methodId, out member);
                                if (!justEffect)
                                    Push(right);
                                if (member != null)
                                    ins.GenericArgument = member;
                                break;
                            }
                            else
                                goto callByTypeGet;
                        }

                        callByTypeGet:
                        right = t.StaticCall(this, argv, PCall.Get, methodId);
                        if (!justEffect)
                            Push(right);
                        break;
                        callByMemberGet:
                        right = ObjectPType._execute(this, member, argv, PCall.Get, methodId, null);
                        if (!justEffect)
                            Push(right);
                        break;

                    case OpCode.sset:
                        _fillArgs(argc, out argv);
                        idx = id.LastIndexOf("::");
                        if (idx < 0)
                            throw new PrexoniteException
                                (
                                "Invalid sget instruction. Does not specify a method.");
                        needToReturn = true;
                        methodId = id.Substring(idx + 2);
                        member = ins.GenericArgument as MemberInfo;
                        if (member != null)
                            goto callByMemberSet;
                        else if ((object) t != null)
                            goto callByTypeSet;
                        else
                        {
                            //Full resolve
                            var typeExpr = id.Substring(0, idx);
                            t = ConstructPType(typeExpr);
                            ins.GenericArgument = t;
                            if (t is ObjectPType)
                            {
                                //Try to get a member info
                                var objT = (ObjectPType) t;
                                objT.StaticCall(this, argv, PCall.Set, methodId, out member);
                                if (member != null)
                                    ins.GenericArgument = member;
                                break;
                            }
                            else
                                goto callByTypeSet;
                        }

                        callByTypeSet:
                        t.StaticCall(this, argv, PCall.Set, methodId);
                        break;
                        callByMemberSet:
                        ObjectPType._execute(this, member, argv, PCall.Set, methodId, null);
                        break;

                        #endregion

                        #endregion

                        #region INDIRECT CALLS

                    case OpCode.indloc:
                        _fillArgs(argc, out argv);
                        pvar = _localVariables[id];
                        if(pvar == null)
                            throw new PrexoniteException("The local variable " + id + " resolved to null in function " + Implementation.Id);
                        left = _localVariables[id].Value;

#if Verbose
                    Console.Write("  " + _toDebug(left) + "(");
                    foreach (PValue arg in argv)
                        Console.Write(_toDebug(arg) + ", ");
                    Console.WriteLine(")");
#endif

//Perform indirect call
                        doIndloc:
                        {
                            var stackAware = left.Value as IStackAware;
                            if (stackAware != null && left.Type is ObjectPType)
                            {
                                _fetchReturnValue = !justEffect;
                                ParentEngine.Stack.AddLast(stackAware.CreateStackContext(this, argv));
                            }
                            else
                            {
                                if (justEffect)
                                    left.IndirectCall(this, argv);
                                else
                                    Push(left.IndirectCall(this, argv));
                            }
                        }

                        needToReturn = true;
                        break;
                    case OpCode.indloci:
                        idx = argc & ushort.MaxValue;
                        argc = (argc & (ushort.MaxValue << 16)) >> 16;
                        _fillArgs(argc, out argv);
                        left = _localVariableArray[idx].Value;
                        goto doIndloc;
                    case OpCode.indglob:
                        _fillArgs(argc, out argv);
                        app = ParentApplication;
                        if (!ParentApplication.Variables.TryGetValue(id, out pvar))
                            throw _globalVariableDoesNotExistException(id); 
                        app.EnsureInitialization(ParentEngine, pvar);
                        left = pvar.Value;

#if Verbose
                    Console.Write("  " + _toDebug(left) + "(");
                    foreach (PValue arg in argv)
                        Console.Write(_toDebug(arg) + ", ");
                    Console.WriteLine(")");
#endif

//Perform indirect call
                        goto doIndloc;

                    case OpCode.indarg:
                        _fillArgs(argc, out argv);
                        left = Pop();
                        goto doIndloc;

                    case OpCode.tail:
                        _fillArgs(argc, out argv);
                        left = Pop();

                        var stack = _parentEngine.Stack;
// ReSharper disable AssignNullToNotNullAttribute
                        stack.Remove(stack.FindLast(this));
// ReSharper restore AssignNullToNotNullAttribute

                        stack.AddLast(Call.CreateStackContext(this, left, argv));
                        return false;

                        #endregion

                        #region ENGINE CALLS

                    case OpCode.func:
                        _fillArgs(argc, out argv);
                        if (ParentEngine.CacheFunctions)
                        {
                            func = (ins.GenericArgument as PFunction) ??
                                   ParentApplication.Functions[id];
                            ins.GenericArgument = func;
                        }
                        else
                        {
                            func = ParentApplication.Functions[id];
                        }
                        if (func == null)
                            throw PrexoniteRuntimeException.CreateRuntimeException
                                (
                                this, "No function with the physical name " + id + " exists.");
#if NoCil
                        FunctionContext fctx =
                            new FunctionContext(
                                ParentEngine, func, argv);

                        _fetchReturnValue = !justEffect;
                        ParentEngine.Stack.AddLast(fctx);
#else
                        if (func.HasCilImplementation)
                        {
                            ReturnMode returnMode;
                            func.CilImplementation(func, this, argv, null, out left, out returnMode);
                            ReturnMode = returnMode;
                            if (!justEffect)
                                Push(left);
                        }
                        else
                        {
                            var fctx =
                                new FunctionContext
                                    (
                                    ParentEngine, func, argv);

                            _fetchReturnValue = !justEffect;
                            ParentEngine.Stack.AddLast(fctx);
                        }
#endif

#if Verbose
                    Console.Write("\n#PSH: " + id + "(");
                    foreach (PValue arg in argv)
                        Console.Write(_toDebug(arg) + ", ");
                    Console.WriteLine(")");
#endif
                        return true;
                        //Force the engine to keep this context on the stack for another cycle
                    case OpCode.cmd:
                        _fillArgs(argc, out argv);
                        needToReturn = true;
                        PCommand cmd;
                        if (ParentEngine.CacheCommands)
                        {
                            cmd = (ins.GenericArgument as PCommand) ?? ParentEngine.Commands[id];
                            ins.GenericArgument = cmd;
                        }
                        else
                        {
                            cmd = ParentEngine.Commands[id];
                        }
                        if (cmd == null)
                            throw new PrexoniteException("Cannot find command " + id + "!");
                        var sa = cmd as IStackAware;
                        if (sa != null)
                        {
                            var cctx = sa.CreateStackContext(this, argv);
                            _fetchReturnValue = !justEffect;
                            ParentEngine.Stack.AddLast(cctx);
                        }
                        else
                        {
                            if (justEffect)
                                cmd.Run(this, argv);
                            else
                            {
#if Verbose
                            val = cmd.Run(this, argv);
                            Console.Write(" =" + _toDebug(val));
                            Push(val);
#else
                                Push(cmd.Run(this, argv));
#endif
                            }
                        }
                        break;

                        #endregion

                        #region FLOW CONTROL

                        //FLOW CONTROL

                        #region JUMPS

                    case OpCode.jump:
                        _pointer = argc;
                        break;
                    case OpCode.jump_t:
                        left = Pop();
                        if (!(left.Value is bool))
                            left = left.ConvertTo(this, PType.Bool);
                        if ((bool) left.Value)
                        {
                            _pointer = argc;
#if Verbose
                        Console.Write(" -> jump");
#endif
                        }
                        break;
                    case OpCode.jump_f:
                        left = Pop();
                        if (!(left.Value is bool))
                            left = left.ConvertTo(this, PType.Bool);
                        if (!(bool) left.Value)
                        {
#if Verbose
                        Console.Write(" -> jump");
#endif
                            _pointer = argc;
                        }
                        break;

                        #endregion

                        #region RETURNS

                    case OpCode.ret_exit:
                        ReturnMode = ReturnMode.Exit;
#if Verbose
                    Console.WriteLine();
#endif
                        return false;
                    case OpCode.ret_value:
                        _returnValue = Pop();
                        ReturnMode = ReturnMode.Exit;
#if Verbose
                    Console.WriteLine("=" + _toDebug(_returnValue));
#endif
                        return false;
                    case OpCode.ret_break:
                        ReturnMode = ReturnMode.Break;
#if Verbose
                    Console.WriteLine();
#endif
                        return false;
                    case OpCode.ret_continue:
                        ReturnMode = ReturnMode.Continue;
#if Verbose
                    Console.WriteLine();
#endif
                        return false;
                    case OpCode.ret_set:
                        _returnValue = Pop();
#if Verbose
                    Console.WriteLine("=" + _toDebug(_returnValue));
#endif
                        break;

                        #endregion

                        #region THROW

                    case OpCode.@throw:
                        left = Pop();
                        t = left.Type;
                        PrexoniteRuntimeException prexc;
                        if (t is StringPType)
                            prexc =
                                PrexoniteRuntimeException.CreateRuntimeException
                                    (
                                    this, (string) left.Value);
                        else if (t is ObjectPType && left.Value is Exception)
                            prexc =
                                PrexoniteRuntimeException.CreateRuntimeException
                                    (
                                    this,
                                    ((Exception) left.Value).Message,
                                    (Exception) left.Value);
                        else
                            prexc =
                                PrexoniteRuntimeException.CreateRuntimeException
                                    (
                                    this, left.CallToString(this));
#if Verbose
                    Console.WriteLine();
#endif
                        throw prexc;

                        #endregion

                        #region LEAVE

                    case OpCode.@try:
                        _isHandlingException.Push(false);
                        break;

                    case OpCode.leave:
                        if(_isHandlingException.Count == 0)
                        {
                            throw new PrexoniteException("Unexpected leave instruction. This happens when jumping to an instruction in a try block from the outside.");
                        }
                        else if (!_isHandlingException.Pop())
                        {
                            //No exception to handle
                            _pointer = argc;
#if Verbose
                        Console.Write(" => Skip catch block.");
#endif
                        }
                        else
                        {
                            if (_currentTry.HasCatch)
                            {
                                //Exception handled by user code
#if Verbose

                                Console.Write(" => execute catch({0}:{1})", 
                                    _currentException.GetType().Name, _currentException.Message);
#endif
                            }
                            else
                            {
                                //Rethrow exception
                                throw _currentException;
                            }
                        }

                        break;

                        #endregion

                        #region EXCEPTION

                    case OpCode.exc:
                        Push(CreateNativePValue(_currentException));
                        break;

                        #endregion

                        #endregion

                        #region STACK MANIPULATION

                        //STACK MANIPULATION
                    case OpCode.pop:
                        if (_stack.Count < argc)
                            _throwInvalidStackException(argc);
                        for (var i = 0; i < argc; i++)
                            Pop(); //Pop to nirvana
                        break;
                    case OpCode.dup:
                        left = Peek();
                        for (var i = 0; i < argc; i++)
                            Push(left);
                        break;
                    case OpCode.rot:
                        var values = (int) ins.GenericArgument;
                        var rotations = argc;
                        var target = new PValue[values];
                        for (var i = 0; i < values; i++)
                            target[(i + rotations)%values] = Pop();
                        for (var i = values - 1; i >= 0; i--)
                            Push(target[i]);
                        break;

                        #endregion
                }

                #endregion

                //Next instruction
#if Verbose
            Console.Write("\n");
#endif
                if (_pointer >= codeLength)
                    return false;
            } while (!needToReturn);

            return _pointer < codeLength;
        }

        private PrexoniteException _globalVariableDoesNotExistException(string id)
        {
            return new PrexoniteException
                (
                "The global variable " + id + " does not exist.");
        }

        #region Exception Handling

        private Exception _currentException;

        private readonly Stack<bool> _isHandlingException = new Stack<bool>();

        /// <summary>
        /// Indicates whether the function context is currently handling an exception or not.
        /// </summary>
        /// <value>True, if the function is currently handling an exception.<br />
        /// False, if the function runs normally.</value>
        public bool IsHandlingException
        {
            get { return _isHandlingException.Peek(); }
        }

        private TryCatchFinallyBlock _currentTry;

        public override bool TryHandleException(Exception exc)
        {
            //Pointer has already been incremented.
            var address = _pointer - 1;

            var block =
                TryCatchFinallyBlock.Closest(address, _implementation.TryCatchFinallyBlocks);

            if (block == null) //No try-catch-finally block handles exceptions at the given address.
                return false;

            _currentException = exc;

            if (block.HasFinally)
            {
#if Verbose
                Console.WriteLine("Exception handled by finally-catch. " + block);
#endif
                _isHandlingException.Pop();
                _isHandlingException.Push(true);
                _pointer = block.BeginFinally;
                _currentTry = block;
            }
            else if (block.HasCatch)
            {
#if Verbose
                Console.WriteLine("Exception handled by catch." + block);
#endif
                _pointer = block.BeginCatch;
            }
            else
            {
                //Fix #9 (Different handling of exception blocks in CIL implementation)
                // The CLR defaults to rethrowing exceptions.
                return false;
            }

            return true;
        }

        #endregion

        #endregion Virtual Machine

        private static string _toDebug(PValue value)
        {
            return PValue.ToDebugString(value);
        }
    }

    [Serializable]
    [DebuggerNonUserCode]
    public class PrexoniteInvalidStackException : PrexoniteException
    {
        public PrexoniteInvalidStackException()
        {
        }

        public PrexoniteInvalidStackException(string message)
            : base(message)
        {
        }

        public PrexoniteInvalidStackException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected PrexoniteInvalidStackException
            (
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}