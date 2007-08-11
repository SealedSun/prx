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
using NoDebug = System.Diagnostics.DebuggerNonUserCodeAttribute;

namespace Prexonite.Types
{
    [PTypeLiteral("Object")]
    public sealed class ObjectPType : PType
    {
        #region Construction

        //Constructor
        [NoDebug()]
        public ObjectPType(Type clrType)
        {
            if (clrType == null)
                throw new ArgumentNullException("clrType");
            _clrType = clrType;
        }

        public ObjectPType(StackContext sctx, PValue[] args)
        {
            if (args == null)
                throw new ArgumentNullException("args"); 
            if(args.Length < 1)
                throw new PrexoniteException("The Object type requires exactly one parameter: the type or name of the type to represent.");

            PValue arg = args[0];
            PValue sarg;
            ObjectPType oT = arg.Type as ObjectPType;
            if(arg.IsNull)
                _clrType = typeof(object);
            else if(oT != null && typeof(Type).IsAssignableFrom(oT.ClrType))
                _clrType = (Type) arg.Value;
            else if(arg.TryConvertTo(sctx, String,false,out sarg))
                _clrType = GetType(sctx, (string) sarg.Value);
            else
                throw new PrexoniteException("The supplied argument parameter (" + arg + ") cannot be used to create an Object<T> type.");
        }

        public ObjectPType(StackContext sctx, string clrTypeName)
        {
            _clrType = GetType(sctx, clrTypeName);
        }

        public static Type GetType(StackContext sctx, string clrTypeName)
        {
            Type result;
            if (TryGetType(sctx, clrTypeName, out result))
                return result;
            else
                throw new PrexoniteException("Cannot resolve ClrType name \"" + clrTypeName + "\".");
        }

        public static bool TryGetType(StackContext sctx, string clrTypeName, out Type result)
        {
            if (clrTypeName == null)
                throw new ArgumentNullException("clrTypeName");
            Assembly[] assemblies = sctx.ParentEngine.GetRegisteredAssemblies();

            result = _getType_forNamesapce(clrTypeName, assemblies);
            if (result != null)
                return true;

            foreach (string ns in sctx.Implementation.ImportedNamespaces)
            {
                string nsName = ns + '.' + clrTypeName;
                result = _getType_forNamesapce(nsName, assemblies);
                if (result != null)
                    return true;
            }
            return false;
        }

        private static Type _getType_forNamesapce(string clrTypeName, Assembly[] assemblies)
        {
            //Try Prexonite and mscorlib
            Type result = Type.GetType(clrTypeName, false, true);
            if (result != null)
                return result;

            //Try registered assemblies
            foreach (Assembly ass in assemblies)
            {
                result = ass.GetType(clrTypeName, false, true);
                if (result != null)
                    return result;
            }

            return null;
        }

        #endregion

        #region ClrType

        private Type _clrType;

        public Type ClrType
        {
            [NoDebug()]
            get { return _clrType; }
        }

        #endregion

        #region Access Interface Implementation

        #region CLR Interop

        public override bool TryDynamicCall(
            StackContext sctx,
            PValue subject,
            PValue[] args,
            PCall call,
            string id,
            out PValue result)
        {
            MemberInfo dummy;
            return TryDynamicCall(sctx, subject, args, call, id, out result, out dummy);
        }

        public bool TryDynamicCall(
            StackContext sctx,
            PValue subject,
            PValue[] args,
            PCall call,
            string id,
            out PValue result,
            out MemberInfo resolvedMember)
        {
            result = null;
            resolvedMember = null;

            if (id == null)
                id = "";

            IObject iobj = subject.Value as IObject;
            if (iobj != null && iobj.TryDynamicCall(sctx, args, call, id, out result))
                return true;

            //Special interop members
            switch(id.ToLowerInvariant())
            {
                case @"\implements":
                    foreach (PValue arg in args)
                    {
                        Type T;
                        if (arg.Type is ObjectPType &&
                            typeof(Type).IsAssignableFrom(((ObjectPType) arg.Type).ClrType))
                            T = (Type) arg.Value;
                        else
                            T = GetType(sctx, arg.CallToString(sctx));

                        if(!T.IsAssignableFrom(ClrType))
                        {
                            result = false;
                            return true;
                        }
                    }
                    result = true;
                    return true;
            }

            call_conditions cond = new call_conditions(sctx, args, call, id);
            MemberTypes mtypes;
            MemberFilter filter;
            if (id.Length != 0)
            {
                filter = _member_filter;

                if (id.LastIndexOf('\\') == 0)
                    return false; //Default index accessors do not accept calling directives
                mtypes = MemberTypes.Event | MemberTypes.Field | MemberTypes.Method | MemberTypes.Property;
            }
            else
            {
                filter = _default_member_filter;
                mtypes = MemberTypes.Property | MemberTypes.Method;
                cond.memberRestriction = new List<MemberInfo>(_clrType.GetDefaultMembers());
                cond.IgnoreId = true;
                if (subject.Value is Array)
                {
                    cond.memberRestriction.AddRange(
                        _clrType.FindMembers(
                            MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance,
                            Type.FilterName, cond.Call == PCall.Get ? "GetValue" : "SetValue"));
                    cond.memberRestriction.AddRange(
                        _clrType.FindMembers(
                            MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance,
                            Type.FilterName, cond.Call == PCall.Get ? "Get" : "Set"));
                }
            }

            //Get public member candidates            
            Stack<MemberInfo> candidates = new Stack<MemberInfo>(_clrType.FindMembers(
                                                                     mtypes, //Member types
                                                                     BindingFlags.Instance | BindingFlags.Public,
                                                                     //Search domain
                                                                     filter, cond)); //Filter

            if (candidates.Count == 1)
                resolvedMember = candidates.Peek();

            bool ret = _try_execute(candidates, cond, subject, out result);
            if (!ret) //Call did not succeed -> member invalid
                resolvedMember = null;

            return ret;
        }

        public override bool TryStaticCall(
            StackContext sctx,
            PValue[] args,
            PCall call,
            string id,
            out PValue result)
        {
            MemberInfo dummy;
            return TryStaticCall(sctx, args, call, id, out result, out dummy);
        }

        public bool TryStaticCall(
            StackContext sctx,
            PValue[] args,
            PCall call,
            string id,
            out PValue result,
            out MemberInfo resolvedMember)
        {
            result = null;
            resolvedMember = null;

            if (id == null)
                id = "";

            call_conditions cond = new call_conditions(sctx, args, call, id);
            MemberTypes mtypes;
            MemberFilter filter;
            if (id.Length != 0)
            {
                filter = _member_filter;
                if (id.LastIndexOf('\\') == 0)
                    return false; //Default index accessors do not accept calling directives
                mtypes = MemberTypes.Event | MemberTypes.Field | MemberTypes.Method | MemberTypes.Property;
            }
            else
            {
                filter = _default_member_filter;
                mtypes = MemberTypes.Property | MemberTypes.Method;
                cond.memberRestriction = new List<MemberInfo>(_clrType.GetDefaultMembers());
                cond.IgnoreId = true;
            }

            //Get member candidates            
            Stack<MemberInfo> candidates = new Stack<MemberInfo>(_clrType.FindMembers(
                                                                     mtypes, //Member types
                                                                     BindingFlags.Static | BindingFlags.Public,
                                                                     //Search domain
                                                                     filter, cond)); //Filter

            if (candidates.Count == 1)
                resolvedMember = candidates.Peek();

            bool ret = _try_execute(candidates, cond, null, out result);
            if (!ret) //Call did not succeed -> member invalid
                resolvedMember = null;
            return ret;
        }

        private bool _try_call_conversion_operator(
            StackContext sctx,
            PValue[] args,
            PCall call,
            string id,
            Type targetType,
            out PValue result)
        {
            result = null;

            if (id == null || id.Length == 0)
                throw new ArgumentException("id may not be null or empty.");

            call_conditions cond = new call_conditions(sctx, args, call, id);
            cond.returnType = targetType;

            //Get member candidates            
            Stack<MemberInfo> candidates = new Stack<MemberInfo>(_clrType.FindMembers(
                                                                     MemberTypes.Method, //Member types
                                                                     BindingFlags.Static | BindingFlags.Public,
                                                                     //Search domain
                                                                     _member_filter, cond)); //Filter

            return _try_execute(candidates, cond, null, out result);
        }

        private static bool _try_execute(Stack<MemberInfo> candidates, call_conditions cond, PValue subject,
                                         out PValue ret)
        {
            ret = null;
            object result;
            while (candidates.Count > 0)
            {
                MemberInfo candidate = candidates.Pop();
                switch (candidate.MemberType)
                {
                    case MemberTypes.Method:
                    case MemberTypes.Constructor:
                        //Try to execute the method
                        MethodBase method = (MethodBase) candidate;
                        ParameterInfo[] parameters = method.GetParameters();
                        object[] cargs = new object[parameters.Length];
                        //The Sctx hack needs to modify the supplied arguments, so we need a copy of the original reference
                        PValue[] sargs = cond.Args;

                        if (_sctx_hack(parameters, cond))
                        {
                            //Add cond.Sctx to the array of arguments
                            sargs = new PValue[sargs.Length + 1];
                            Array.Copy(cond.Args, 0, sargs, 1, cond.Args.Length);
                            sargs[0] = Object.CreatePValue(cond.Sctx);
                        }

                        for (int i = 0; i < cargs.Length; i++)
                        {
                            PValue arg = sargs[i];
                            if (!(arg.IsTypeLocked || arg.IsNull)) //Neither Type-locked nor null
                            {
                                Type P = parameters[i].ParameterType;
                                Type A = arg.ClrType;
                                if (!(P.Equals(A) || P.IsAssignableFrom(A))) //Is conversion needed?
                                {
                                    if (!arg.TryConvertTo(cond.Sctx, P, false, out arg)) //Try to convert
                                        goto failed; //can't use break; because of the surrounding for-loop
                                }
                            }
                            cargs[i] = arg.Value;
                        }

                        //All conversions were succesful, ready to call the method
                        if (method is ConstructorInfo)
                        {
                            result = ((ConstructorInfo) method).Invoke(cargs);
                        }
                        else
                        {
                            if (subject == null)
                                result = method.Invoke(null, cargs);
                            else
                                result = method.Invoke(subject.Value, cargs);
                        }
                        goto success;

                        failed: //The method cannot be called. Go on to the next candidate
                        break;
                    case MemberTypes.Field:
                        //Do field access
                        FieldInfo field = (FieldInfo) candidate;
                        if (cond.Call == PCall.Get)
                            if (subject == null)
                                result = field.GetValue(null);
                            else
                                result = field.GetValue(subject.Value);
                        else
                        {
                            PValue arg = cond.Args[0];
                            if (!(arg.IsTypeLocked || arg.IsNull)) //Neither Type-locked nor null
                            {
                                Type P = field.FieldType;
                                Type A = arg.ClrType;
                                if (!(P.Equals(A) || P.IsAssignableFrom(A))) //Is conversion needed?
                                {
                                    if (!arg.TryConvertTo(cond.Sctx, P, false, out arg)) //Try to convert
                                        break;
                                }
                            }
                            if (subject == null)
                                field.SetValue(null, arg.Value);
                            else
                                field.SetValue(subject.Value, arg.Value);
                            result = null;
                        }
                        goto success;
                    case MemberTypes.Property:
                        //Push accessor method
                        PropertyInfo property = (PropertyInfo) candidate;
                        if (cond.Call == PCall.Get)
                            candidates.Push(property.GetGetMethod());
                        else
                            candidates.Push(property.GetSetMethod());
                        break;
                    case MemberTypes.Event:
                        //Push requested method
                        EventInfo info = (EventInfo) candidate;
                        if (cond.Directive == "" || Engine.DefaultStringComparer.Compare(cond.Directive, "Raise") == 0)
                        {
                            candidates.Push(info.GetRaiseMethod());
                        }
                        else if (Engine.DefaultStringComparer.Compare(cond.Directive, "Add") == 0)
                        {
                            candidates.Push(info.GetAddMethod());
                        }
                        else if (Engine.DefaultStringComparer.Compare(cond.Directive, "Remove") == 0)
                        {
                            candidates.Push(info.GetRemoveMethod());
                        }
                        break;
                }
            }

            return false;

            success:
            if (cond.Call == PCall.Get)
            {
                //We'll let the executing engin decide which ptype suits best:
                ret = cond.Sctx.CreateNativePValue(result);
            }
            return true;
        }

        internal static PValue _execute(StackContext sctx, MemberInfo candidate, PValue[] args, PCall call, string id,
                                        PValue subject)
        {
            Stack<MemberInfo> candidates = new Stack<MemberInfo>();
            candidates.Push(candidate);
            PValue ret;
            if (!_try_execute(candidates, new call_conditions(sctx, args, call, id), subject, out ret))
                throw new InvalidCallException("Cannot call resolved member " + candidate);
            return ret;
        }

        [NoDebug()]
        private class call_conditions
        {
            public StackContext Sctx;
            public PValue[] Args;
            public PCall Call;
            public string Id;
            public bool IgnoreId;
            public string Directive;
            public Type returnType;
            public List<MemberInfo> memberRestriction;

            public call_conditions(StackContext sctx, PValue[] args, PCall call, string id)
            {
                if (sctx == null)
                    throw new ArgumentNullException("sctx");
                Sctx = sctx;
                Args = args ?? new PValue[] {};
                Call = call;
                Id = id;
                Directive = null;
                returnType = null;
                memberRestriction = null;

                //look for special calling directives
                int idx = id.LastIndexOf('\\');
                if (idx > 0) //calling directive found
                {
                    Id = id.Substring(0, idx);
                    Directive = id.Substring(idx + 1);
                }
            }
        }

        private static bool _default_member_filter(MemberInfo candidate, object arg)
        {
            PropertyInfo property = candidate as PropertyInfo;
            MethodInfo method = candidate as MethodInfo;
            call_conditions cond = (call_conditions) arg;

            //Criteria No.1: Default indices are called "Item" by convention
            if (!(
                     //Is default member or...
                 (cond.memberRestriction != null && cond.memberRestriction.Contains(candidate)) ||
                 //is called "item"
                 candidate.Name.Equals("Item", StringComparison.OrdinalIgnoreCase)
                 ))
                return false;

            if (property != null)
            {
                if (cond.Call == PCall.Get)
                {
                    if (!property.CanRead)
                        return false;
                    return _method_filter(property.GetGetMethod(), cond);
                }
                else //cond.Call == PCall.Set
                {
                    if (!property.CanWrite)
                        return false;
                    return _method_filter(property.GetSetMethod(), cond);
                }
            }
            else if (method != null)
            {
                return _method_filter(method, cond);
            }
            else
                throw new InvalidCallException(
                    "_default_member_filter cannot process anything but properties and methods. Candidate however was of type " +
                    candidate.GetType() + ".");
        }

        private static bool _member_filter(MemberInfo candidate, object arg)
        {
            call_conditions cond = (call_conditions) arg;
            //Criteria No.1: The members name (may be supressed)
            if (!(cond.IgnoreId || candidate.Name.Equals(cond.Id, StringComparison.OrdinalIgnoreCase)))
                return false;

            //Criteria No.2: The number of formal parameters
            //Set = min 1 Argument
            if (cond.Call == PCall.Set && cond.Args.Length == 0)
                return false;
            if (candidate is FieldInfo)
            {
                //Get+Field = 0 Parameters, Set+Field = 1 Parameter
                if (cond.Call == PCall.Get)
                {
                    if (cond.Args.Length == 0)
                        return true;
                    else
                        return false;
                }
                else
                {
                    if (cond.Args.Length == 1)
                    {
                        //Ensure that type-locked values are acceptable
                        if (cond.Args[0].IsTypeLocked)
                        {
                            Type P = (candidate as FieldInfo).FieldType;
                            Type A = cond.Args[0].ClrType;
                            if (!(P.Equals(A) || P.IsAssignableFrom(A))) //Neiter Equal nor assignable
                                return false;
                        }

                        return true;
                    }
                    else
                        return false;
                }
            }
            else if (candidate is PropertyInfo)
            {
                PropertyInfo property = candidate as PropertyInfo;
                if (cond.Call == PCall.Get)
                {
                    if (!property.CanRead)
                        return false;
                    else
                        return _method_filter(property.GetGetMethod(), cond);
                }
                else //cond.Call == PCall.Set
                {
                    if (!property.CanWrite)
                        return false;
                    else
                        return _method_filter(property.GetSetMethod(), cond);
                }
            }
            else if (candidate is MethodInfo)
            {
                return _method_filter(candidate as MethodInfo, cond);
            }
            else if (candidate is EventInfo)
            {
                EventInfo info = candidate as EventInfo;
                if (cond.Directive == "" || Engine.DefaultStringComparer.Compare(cond.Directive, "Raise") == 0)
                {
                    return _method_filter(info.GetRaiseMethod(), cond);
                }
                else if (Engine.DefaultStringComparer.Compare(cond.Directive, "Add") == 0)
                {
                    return _method_filter(info.GetAddMethod(), cond);
                }
                else if (Engine.DefaultStringComparer.Compare(cond.Directive, "Remove") == 0)
                {
                    return _method_filter(info.GetRemoveMethod(), cond);
                }
                else
                    return false;
            }
            else //Do not support other members than fields, properties, methods and events
                return false;
        }

        /// <summary>
        /// Checks whether the StackContext hack can be applied.
        /// </summary>
        /// <param name="parameters">The parameters array to check.</param>
        /// <param name="cond">The call_condition object for the current call.</param>
        /// <returns>True if the the hack can be applied, otherwise false.</returns>
        private static bool _sctx_hack(ParameterInfo[] parameters, call_conditions cond)
        {
            //StackContext Hack
            //NOTE: This might be the source of strange problems!
            //If the one argument is missing and the first formal parameter is a StackContext,
            //supply the StackContext received in cond.sctx.
            return (
                       //There have to be parameters
                   parameters.Length > 0 &&
                   //One argument must be missing
                   cond.Args.Length + 1 == parameters.Length &&
                   //First parameter must be a StackContext
                   typeof(StackContext).IsAssignableFrom(parameters[0].ParameterType));
        }

        private static bool _method_filter(MethodBase method, call_conditions cond)
        {
            ParameterInfo[] parameters = method.GetParameters();

            //Hide Sctx parameter
            if (_sctx_hack(parameters, cond))
            {
                ParameterInfo[] relevantParameters = new ParameterInfo[parameters.Length - 1];
                Array.Copy(parameters, 1, relevantParameters, 0, relevantParameters.Length);
                parameters = relevantParameters;
            }

            //Criteria No.1: The number of arguments has to match the number of parameters
            if (cond.Args.Length != parameters.Length)
                return false;

            //Criteria No.2: All Type-Locked arguments must match without a conversion
            for (int i = 0; i < parameters.Length; i++)
            {
                if (cond.Args[i].IsTypeLocked)
                {
                    Type P = parameters[i].ParameterType;
                    Type A = cond.Args[i].ClrType;
                    if (!(P.Equals(A) || P.IsAssignableFrom(A))) //Neiter Equal nor assignable
                        return false;
                }
            }

            //optional Criteria No.3: Return types must match
            if (cond.returnType != null && method is MethodInfo)
            {
                MethodInfo methodEx = method as MethodInfo;
                if (!(methodEx.ReturnType.Equals(cond.returnType) ||
                      cond.returnType.IsAssignableFrom(methodEx.ReturnType)))
                {
                    return false;
                }
            }

            //The method is a candidate
            return true;
        }

        public override bool IndirectCall(StackContext sctx, PValue subject, PValue[] args, out PValue result)
        {
            result = null;
            IIndirectCall icall = subject.Value as IIndirectCall;
            if (icall != null)
                result = icall.IndirectCall(sctx, args) ?? PType.Null.CreatePValue();

            return result != null;
        }

        #endregion

        #region Calls

        public PValue DynamicCall(StackContext sctx, PValue subject, PValue[] args, PCall call, string id,
                                  out MemberInfo resolvedMember)
        {
            PValue result;
            if (!TryDynamicCall(sctx, subject, args, call, id, out result, out resolvedMember))
                throw new InvalidCallException(
                    "Cannot resolve CLR call '" + id + "' on object of type " +
                    (subject.IsNull ? "null" : subject.ClrType.FullName) + ".");
            return result;
        }

        public override PValue DynamicCall(StackContext sctx, PValue subject, PValue[] args, PCall call, string id)
        {
            MemberInfo dummy;
            return DynamicCall(sctx, subject, args, call, id, out dummy);
        }

        public PValue StaticCall(StackContext sctx, PValue[] args, PCall call, string id, out MemberInfo resolvedMember)
        {
            PValue result;
            if (!TryStaticCall(sctx, args, call, id, out result, out resolvedMember))
                throw new InvalidCallException(
                    "Cannot resolve static CLR call '" + id + "' on type " + ClrType + ".");
            return result;
        }

        public override PValue StaticCall(StackContext sctx, PValue[] args, PCall call, string id)
        {
            MemberInfo dummy;
            return StaticCall(sctx, args, call, id, out dummy);
        }

        public override bool TryContruct(StackContext sctx, PValue[] args, out PValue result)
        {
            MemberInfo dummy;
            return TryContruct(sctx, args, out result, out dummy);
        }

        public bool TryContruct(StackContext sctx, PValue[] args, out PValue result, out MemberInfo resolvedMember)
        {
            call_conditions cond = new call_conditions(sctx, args, PCall.Get, "");
            cond.IgnoreId = true;

            //Get member candidates            
            Stack<MemberInfo> candidates = new Stack<MemberInfo>();
            foreach (ConstructorInfo ctor in _clrType.GetConstructors())
            {
                if (_method_filter(ctor, cond))
                    candidates.Push(ctor);
            }

            resolvedMember = null;
            if (candidates.Count == 1)
                resolvedMember = candidates.Peek();

            bool ret = _try_execute(candidates, cond, null, out result);
            if (!ret)
                resolvedMember = null;

            return ret;
        }

        #endregion

        #region Operators

        public override bool Addition(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_Addition", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get, "op_Addition",
                                                out result);
        }

        public override bool Subtraction(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_Subtraction", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get,
                                                "op_Subtraction", out result);
        }

        public override bool Multiply(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_Multiply", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get, "op_Multiply",
                                                out result);
        }

        public override bool Division(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_Division", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get, "op_Division",
                                                out result);
        }

        public override bool Modulus(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_Modulus", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get, "op_Modulus",
                                                out result);
        }

        public override bool BitwiseAnd(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_BitwiseAnd", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get,
                                                "op_BitwiseAnd", out result);
        }

        public override bool BitwiseOr(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_BitwiseOr", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get,
                                                "op_BitwiseOr", out result);
        }

        public override bool ExclusiveOr(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_ExclusiveOr", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get,
                                                "op_ExclusiveOr", out result);
        }

        public override bool Equality(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_Equality", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get, "op_Equality",
                                                out result);
        }

        public override bool Inequality(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_Inequality", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get,
                                                "op_Inequality", out result);
        }

        public override bool GreaterThan(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_GreaterThan", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get,
                                                "op_GreaterThan", out result);
        }

        public override bool GreaterThanOrEqual(StackContext sctx, PValue leftOperand, PValue rightOperand,
                                                out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_GreaterThanOrEqual",
                              out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get,
                                                "op_GreaterThanOrEqual", out result);
        }

        public override bool LessThan(StackContext sctx, PValue leftOperand, PValue rightOperand, out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_LessThan", out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get, "op_LessThan",
                                                out result);
        }

        public override bool LessThanOrEqual(StackContext sctx, PValue leftOperand, PValue rightOperand,
                                             out PValue result)
        {
            return
                TryStaticCall(sctx, new PValue[] {leftOperand, rightOperand}, PCall.Get, "op_LessThanOrEqual",
                              out result) ||
                rightOperand.Type.TryStaticCall(sctx, new PValue[] {rightOperand, leftOperand}, PCall.Get,
                                                "op_LessThanOrEqual", out result);
        }

        public override bool UnaryNegation(StackContext sctx, PValue operand, out PValue result)
        {
            return TryStaticCall(sctx, new PValue[] {operand}, PCall.Get, "op_UnaryNegation", out result);
        }

        public override bool LogicalNot(StackContext sctx, PValue operand, out PValue result)
        {
            return TryStaticCall(sctx, new PValue[] {operand}, PCall.Get, "op_LogicalNot", out result);
        }

        public override bool OnesComplement(StackContext sctx, PValue operand, out PValue result)
        {
            return TryStaticCall(sctx, new PValue[] {operand}, PCall.Get, "op_OnesComplement", out result);
        }

        public override bool Increment(StackContext sctx, PValue operand, out PValue result)
        {
            return TryStaticCall(sctx, new PValue[] {operand}, PCall.Get, "op_Increment", out result);
        }

        public override bool Decrement(StackContext sctx, PValue operand, out PValue result)
        {
            return TryStaticCall(sctx, new PValue[] {operand}, PCall.Get, "op_Decrement", out result);
        }

        #endregion

        #region Conversion

        protected override bool InternalConvertTo(StackContext sctx, PValue subject, PType target, bool useExplicit,
                                                  out PValue result)
        {
            PValue[] arg = new PValue[] {subject};
            ObjectPType objT = target as ObjectPType;
            result = null;
            if (target is IntPType)
            {
                if (subject.Value is IConvertible)
                {
                    try
                    {
                        result = Int.CreatePValue(Convert.ToInt32(subject.Value));
                        return true;
                    }
                    catch (InvalidCastException)
                    {
                        //ignore invalid cast exceptions
                    }
                }
                else if (_try_clr_convert_to(sctx, subject, typeof(int), useExplicit, out result))
                    return true;

                return false;
            }
            else if (target is RealPType)
            {
                if (subject.Value is IConvertible)
                {
                    try
                    {
                        result = Real.CreatePValue(Convert.ToDouble(subject.Value));
                        return true;
                    }
                    catch (InvalidCastException)
                    {
                        //ignore invalid cast exceptions
                    }
                }

                if (_try_clr_convert_to(sctx, subject, typeof(double), useExplicit, out result))
                    return true;

                return false;
            }
            else if (target is StringPType)
            {
                return _try_clr_convert_to(sctx, subject, typeof(string), useExplicit, out result);
            }
            else if (target is BoolPType)
            {
                // ::op_True > ::op_Implicit > ::op_Explicit
                PValue res;
                if (!TryStaticCall(sctx, arg, PCall.Get, "op_True", out res))
                    if (!_try_clr_convert_to(sctx, subject, typeof(bool), useExplicit, out res))
                        //An object is true by default
                        result = new PValue(true, Bool);
                    else if (res.Value is bool)
                        result = new PValue((bool) res.Value, Bool);
                    else
                        result = new PValue(res != null, Bool);

                return true;
            }
            else if (objT != null)
            {
                if (objT.ClrType.IsInterface &&
                    ClrType.FindInterfaces(delegate(Type T, object o) { return T.Name == o as string; },
                                           objT.ClrType.Name).Length == 1)
                {
                    result = objT.CreatePValue(subject.Value);
                    return result != null && !(result.Type == Null);
                }
                else
                    return _try_clr_convert_to(sctx, subject, objT.ClrType, useExplicit, out result);
            }
            else
                return false;
        }

        private bool _try_clr_convert_to(StackContext sctx, PValue subject, Type target, bool useExplicit,
                                         out PValue result)
        {
            PValue[] arg = new PValue[] {subject};
            if (_try_call_conversion_operator(sctx, arg, PCall.Get, "op_Implicit", target, out result) ||
                (useExplicit && _try_call_conversion_operator(sctx, arg, PCall.Get, "op_Explicit", target, out result)))
                return true;
            else
                return false;
        }

        protected override bool InternalConvertFrom(StackContext sctx, PValue subject, bool useExplicit,
                                                    out PValue result)
        {
            return _try_clr_convert_to(sctx, subject, ClrType, useExplicit, out result);
        }

        #endregion

        #region Class

        protected override bool InternalIsEqual(PType otherType)
        {
            return (otherType is ObjectPType && ((ObjectPType) otherType)._clrType == _clrType);
        }

        public override int GetHashCode()
        {
            return _CombineHashes(_code, _clrType.GetHashCode());
        }

        public const string Literal = "Object";

        private const int _code = -410320954;

        public override string ToString()
        {
            return Literal + "(\"" + StringPType.Escape(_clrType.FullName) + "\")";
        }

        #endregion

        #endregion
    }
}