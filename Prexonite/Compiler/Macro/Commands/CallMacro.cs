﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Prexonite.Commands;
using Prexonite.Commands.Core;
using Prexonite.Compiler.Ast;
using Prexonite.Types;

namespace Prexonite.Compiler.Macro.Commands
{
    public class CallMacro : PartialMacroCommand
    {
        public const string Alias = @"call\macro";

        #region Singleton pattern

        private static readonly CallMacro _instance = new CallMacro();

        public static CallMacro Instance
        {
            get { return _instance; }
        }

        private CallMacro() : base(Alias)
        {
        }

        #endregion

        public static KeyValuePair<string,CallMacroPerform> GetHelperCommands(Loader ldr)
        {
            return
                new KeyValuePair<string, CallMacroPerform>(CallMacroPerform.Alias, new CallMacroPerform(ldr));
        }

        #region Call\Macro\Perform

        public class CallMacroPerform : PCommand
        {
// ReSharper disable MemberHidesStaticFromOuterClass
            public const string Alias = @"call\macro\perform";
// ReSharper restore MemberHidesStaticFromOuterClass
            private readonly Loader _loader;

            public CallMacroPerform(Loader loader)
            {
                _loader = loader;
            }

            #region Overrides of PCommand

            private static SymbolInterpretations _inferInterpretation(PValue arg)
            {
                const string notRecognized = "{0} is not recognizable as a macro.";
                if (!(arg.Type is ObjectPType))
                    throw new PrexoniteException(string.Format(
                        notRecognized, arg));


                var raw = arg.Value;
                var cmd = raw as MacroCommand;
                var func = raw as PFunction;

                if (cmd != null)
                    return SymbolInterpretations.MacroCommand;
                else if (func != null && func.IsMacro)
                    return SymbolInterpretations.Function;
                else
                    throw new PrexoniteException(string.Format(
                        notRecognized, arg));
            }

            private const int _callingConventionArgumentsCount = 4;

            public override PValue Run(StackContext sctx, PValue[] args)
            {
                if (args.Length < _callingConventionArgumentsCount)
                    throw new PrexoniteException("Id of macro implementation, effect flag, call type and/or context missing.");

                string id;
                SymbolInterpretations macroInterpretation;

                //Parse arguments
                _getMacro(sctx, args[0], out id, out macroInterpretation);
                var context = _getContext(args[1]);
                var call = _getCallType(args[2]);
                var justEffect = _getEffectFlag(args[3]);

                // Prepare macro
                var target = _loader.FunctionTargets[context.Function];
                var argList = Call.FlattenArguments(sctx, args, _callingConventionArgumentsCount);
                _detectRuntimeValues(argList);

                var inv = new AstMacroInvocation(context.Invocation.File, context.Invocation.Line,
                                                 context.Invocation.Column, id,
                                                 macroInterpretation);
                inv.Arguments.AddRange(argList.Select(p => (IAstExpression) p.Value));
                inv.Call = call;

                //Execute the macro
                MacroSession macroSession = null;
                try
                {
                    macroSession = target.AcquireMacroSession();

                    return sctx.CreateNativePValue(
                        macroSession.ExpandMacro(inv, justEffect));
                }
                finally
                {
                    if(macroSession != null)
                        target.ReleaseMacroSession(macroSession);
                }
            
            }

            private static void _detectRuntimeValues(IEnumerable<PValue> argList)
            {
                var offender =
                    argList.FirstOrDefault(
                        p => !(p.Type is ObjectPType) || !(p.Value is IAstExpression));
                if (offender != null)
                    throw new PrexoniteException(
                        string.Format(
                            "Macros cannot have runtime values as arguments in call to {1}. {0} is not an AST node.",
                            offender,Alias));
            }

            private static bool _getEffectFlag(PValue rawEffectFlag)
            {
                if(rawEffectFlag.Type != PType.Bool)
                    throw new PrexoniteException(string.Format("Effect flag is missing in call to {0}.", Alias));
                return (bool) rawEffectFlag.Value;
            }

            private static PCall _getCallType(PValue rawCallType)
            {
                if(!(rawCallType.Type is ObjectPType && rawCallType.Value is PCall))
                    throw new PrexoniteException(string.Format("Call type is missing in call to {0}.", Alias));
                return (PCall) rawCallType.Value;
            }

            private static MacroContext _getContext(PValue rawContext)
            {
                var context = rawContext.Value as MacroContext;
                if(!(rawContext.Type is ObjectPType) || context == null)
                    throw new PrexoniteException(string.Format("Macro context is missing in call to {0}.", Alias));
                return context;
            }

            private static void _getMacro(StackContext sctx, PValue rawMacro, out string id, out SymbolInterpretations macroInterpretation)
            {
                var list = rawMacro.Value as List<PValue>;
                if(rawMacro.Type == PType.List && list != null)
                {
                    if (list.Count < 2)
                        throw new PrexoniteException(
                            string.Format(
                                "First argument to {0} is a list, it must contain the macro id and its interpretation.",
                                CallMacro.Alias));

                    id = list[0].Value as string;
                    if (list[0].Type != PType.String || id == null)
                        throw new PrexoniteException(string.Format("First argument must be id in call to {0}.", Alias));

                    if (!(list[1].Type is ObjectPType) || !(list[1].Value is SymbolInterpretations))
                        throw new PrexoniteException(string.Format("Second argument must be symbol interpretation in call to {0}.", Alias));
                    macroInterpretation = (SymbolInterpretations)list[1].Value;
                }
                else
                {
                    id = rawMacro.DynamicCall(sctx, Cil.Runtime.EmptyPValueArray, PCall.Get,
                        PFunction.IdKey).ConvertTo<string>(sctx, false);
                    macroInterpretation = _inferInterpretation(rawMacro);
                }
            }

            #endregion

            private readonly PartialCallMacroPerform _partial = new PartialCallMacroPerform();

            public PartialCallMacroPerform Partial
            {
                [DebuggerStepThrough]
                get { return _partial; }
            }

            public class PartialCallMacroPerform : PartialCallWrapper
            {
// ReSharper disable MemberHidesStaticFromOuterClass
                public const string Alias = @"call\macro\impl";
// ReSharper restore MemberHidesStaticFromOuterClass

                public PartialCallMacroPerform()
                    : base(Alias,CallMacroPerform.Alias,SymbolInterpretations.Command)
                {
                    
                }

                protected override int GetPassThroughArguments(MacroContext context)
                {
                    return _callingConventionArgumentsCount + 1; //Take reference to call\macro\perform into account.
                }
            }
        }

        #endregion

        #region Overrides of MacroCommand

        protected override void DoExpand(MacroContext context)
        {
            var prepareCall = _assembleCallPerform(context);

            //null indicates failure, error has already been reported.
            if(prepareCall == null)
                return;

            context.Block.Expression = prepareCall;
        }

        #region Helper routines

        /// <summary>
        /// Establishes macro context and parses arguments.
        /// </summary>
        /// <param name="context">The macro context.</param>
        /// <returns>The call to call\macro\perform expression on success; null otherwise.</returns>
        private static AstMacroInvocation _assembleCallPerform(MacroContext context)
        {
            if (context.Invocation.Arguments.Count == 0)
            {
                context.ReportMessage(ParseMessageSeverity.Error,
                    "call\\macro must be supplied a macro reference.");
                return null;
            }

            if (!context.CallerIsMacro())
            {
                context.ReportMessage(ParseMessageSeverity.Error,
                    string.Format(
                        "call\\macro called from {0}. " +
                            "call\\macro can only be called from a macro context, i.e., from a macro function or an " +
                                "inner function of a macro.", context.Function.LogicalId));
                return null;
            }

            context.EstablishMacroContext();

            IAstExpression call;
            IAstExpression justEffect;
            IAstExpression[] args;
            IAstExpression macroSpec;

            if (!_parseArguments(context, out call, out justEffect, out args, false, out macroSpec))
                return null;

            // [| call\macro\prepare_macro("$macroId", $macroInterpretation, context, $call, $justEffect, $args...) |]
            return _prepareMacro(context, macroSpec, call, justEffect, args);
        }

        private static AstMacroInvocation _prepareMacro(MacroContext context, IAstExpression macroSpec, IAstExpression call, IAstExpression justEffect, IEnumerable<IAstExpression> args)
        {
            var getContext = context.CreateGetSetSymbol(
                SymbolInterpretations.LocalReferenceVariable, PCall.Get, MacroAliases.ContextAlias);
            var prepareCall = context.CreateMacroInvocation(context.Call,
                CallMacroPerform.PartialCallMacroPerform.Alias, SymbolInterpretations.MacroCommand, macroSpec,
                getContext, call,
                justEffect);
            prepareCall.Arguments.AddRange(args);
            return prepareCall;
        }

        private static bool _parseArguments(MacroContext context, out IAstExpression call, out IAstExpression justEffect, out IAstExpression[] args, bool isPartialApplication, out IAstExpression macroSpec)
        {
            /* call(macroRef,...) = call([],macroRef,[false],...);
             * call([],macroRef,[je],...) = call([],macroRef,[je,context.Call],...);
             * call([],macroRef,[je,c],...) = { macroId := macroRef.Id; 
             *                                  macroInterpretation := interpretation(macroRef); 
             *                                  call := c; 
             *                                  justEffect := je 
             *                                }
             * call([proto(...1)],...2) = call([],from_proto(proto),[false,PCall.Get],[...1],...2);
             * call([proto(...1) = x],...2) = call([],from_proto(proto),[false,PCall.Set],[...1],...2,[x]);
             * call([proto(...1),je],...2) = call([],from_proto(proto),[je,PCall.Get],[...1],...2);
             * call([proto(...1) = x,je],...2) = call([],from_proto(proto),[je,PCall.Set],[...1],...2,[x]);
             * call([proto(...1),je,c],...2) = call([],from_proto(proto),[je,c],[...1],...2);
             */

            var inv = context.Invocation;
            justEffect = new AstConstant(inv.File, inv.Line,
                inv.Column, false);
            call = PCall.Get.EnumToExpression(context.Invocation);

            var invokeSpec = inv.Arguments[0];
            var listSpec = invokeSpec as AstListLiteral;
            if (listSpec == null)
            {
                // - Macro reference specified as expression that evaluates to an actual macro reference

                args = inv.Arguments.Skip(1).ToArray();
                macroSpec = invokeSpec;
                return _parseReference(context, inv.Arguments[0], isPartialApplication);
            }
            else if (listSpec.Elements.Count == 0)
            {
                // - Macro reference specified as expression that evaluates to an actual macro reference
                // - followed by a list of options

                macroSpec = invokeSpec;

                AstListLiteral optionsRaw;
                if (inv.Arguments.Count < 3 ||
                    (optionsRaw = inv.Arguments[2] as AstListLiteral) == null)
                {
                    _errorUsageFullRef(context, isPartialApplication);
                    args = null;
                    macroSpec = null;
                    return false;
                }

                //first option: justEffect
                if (optionsRaw.Elements.Count >= 1)
                    justEffect = optionsRaw.Elements[0];

                //second option: call type
                if (optionsRaw.Elements.Count >= 2)
                    call = optionsRaw.Elements[1];

                //args: except first 3
                args = inv.Arguments.Skip(3).ToArray();

                return _parseReference(context, inv.Arguments[1],
                    isPartialApplication);
            }
            else
            {
                // - Macro reference specified as a prototype
                // - includes list of options

                var specProto = listSpec.Elements[0];
                PCall protoCall;
                IList<IAstExpression> protoArguments;
                if (
                    !_parsePrototype(context, isPartialApplication, specProto, out protoCall,
                        out protoArguments, out macroSpec))
                {
                    args = null;
                    return false;
                }

                //first option: justEffect
                if (listSpec.Elements.Count >= 2)
                    justEffect = listSpec.Elements[1];

                //second option: call type
                if (listSpec.Elements.Count >= 3)
                    call = listSpec.Elements[2];
                else
                {
                    call = protoCall.EnumToExpression(specProto);
                }

                //args: lift and pass prototype arguments, special care for set

                var setArgs = protoCall == PCall.Set
                    ? protoArguments.Last()
                    : null;
                var getArgs = protoCall == PCall.Set
                    ? protoArguments.Take(protoArguments.Count - 1)
                    : protoArguments;

                if(getArgs.Any(a => !_ensureExplicitPlaceholder(context, a)))
                {
                    args = new IAstExpression[] {};
                    return false;
                }

                IEnumerable<IAstExpression> getArgsLit;
                if(getArgs.Count() > 0){
                    var getArgsLitNode = new AstListLiteral(specProto.File, specProto.Line,
                        specProto.Column);
                    getArgsLitNode.Elements.AddRange(getArgs);
                    getArgsLit = getArgsLitNode.Singleton();
                } else
                {
                    getArgsLit = Enumerable.Empty<IAstExpression>();
                }

                IEnumerable<IAstExpression> setArgsLit;
                if (setArgs != null)
                {
                    if (!_ensureExplicitPlaceholder(context, setArgs))
                    {
                        args = new IAstExpression[] {};
                        return false;
                    }
                    var lit = new AstListLiteral(setArgs.File, setArgs.Line, setArgs.Column);
                    lit.Elements.Add(setArgs);
                    setArgsLit = lit.Singleton();
                }
                else
                {
                    setArgsLit = Enumerable.Empty<IAstExpression>();
                }

                args = getArgsLit
                    .Append(inv.Arguments.Skip(1))
                    .Append(setArgsLit).ToArray();

                return true;
            }
        }

        private static bool _ensureExplicitPlaceholder(MacroContext context, IAstExpression arg)
        {
            var setPlaceholder = arg as AstPlaceholder;
            if(setPlaceholder != null && !setPlaceholder.Index.HasValue)
            {
                context.ReportMessage(ParseMessageSeverity.Error,
                    string.Format(
                        "Due to an internal limitation, " +
                            "the index of a placeholder in the macro prototype's argument list inside {0} cannot be inferred. " +
                                "Specify the placeholders index explicitly (e.g.,  ?0, ?1, etc.).",
                        Alias), setPlaceholder);
                return false;
            }
            return true;
        }

        private static bool _parsePrototype(MacroContext context, bool isPartialApplication, IAstExpression specProto, out PCall protoCall, out IList<IAstExpression> protoArguments, out IAstExpression macroSpec)
        {
            var proto = specProto as AstMacroInvocation;
            if (proto != null)
            {
                macroSpec = _getMacroSpecExpr(context, proto);
                protoCall = proto.Call;
                protoArguments = proto.Arguments;
            }
            else if (specProto.IsPlaceholder())
            {
                //As an exception, allow a placeholder here
                macroSpec = specProto;
                protoCall = PCall.Get;
                protoArguments = new List<IAstExpression>();
            }
            else
            {
                _errorUsagePrototype(context, isPartialApplication);
                macroSpec = null;
                protoCall = PCall.Get;
                protoArguments = null;
                return false;
            }
            return true;
        }

        private static IAstExpression _getMacroSpecExpr(MacroContext context, AstMacroInvocation proto)
        {
            //macroId: as a constant
            var macroId = context.CreateConstant(proto.MacroId);

            //macroInterpretation: as an expression
            var macroInterpretation = proto.Interpretation.EnumToExpression(proto);

            var listLit = new AstListLiteral(context.Invocation.File, context.Invocation.Line,
                context.Invocation.Column);
            listLit.Elements.Add(macroId);
            listLit.Elements.Add(macroInterpretation);

            return listLit;
        }

        private static void _errorUsagePrototype(MacroContext context, bool isPartialApplication)
        {
            context.ReportMessage(ParseMessageSeverity.Error,
                string.Format(
                    "Used in this way, {0} has the form {0}([macroPrototype(...),justEffect?,call?],...).",
                    Alias));
        }

        private static bool _parseReference(MacroContext context, IAstExpression macroRef, bool isPartialApplication)
        {
            if(macroRef.IsPlaceholder())
            {
                context.ReportMessage(ParseMessageSeverity.Error, "The macro prototype must be known at compile-time, it must not be a placeholder.");
                return false;
            }

            return true;
        }

        private static void _errorUsageFullRef(MacroContext context, bool isPartialApplication)
        {
            context.ReportMessage(ParseMessageSeverity.Error, "Used in this way, {0} has the form {0}([],macroRef,[justEffect?,call?],...).");
        }

        #endregion
        
        #endregion

        #region Overrides of PartialMacroCommand

        protected override bool DoExpandPartialApplication(MacroContext context)
        {
            var prepareCall = _assembleCallPerform(context);

            //null indicates failure, error has already been reported.
            if (prepareCall == null)
                return true;

            context.Block.Expression = prepareCall;

            return true;
        }

        #endregion
    }
}