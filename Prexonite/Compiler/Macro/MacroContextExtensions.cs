﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Prexonite.Compiler.Ast;
using Prexonite.Types;

namespace Prexonite.Compiler.Macro
{
    public static class MacroContextExtensions
    {
        public static AstGetSetSymbol CreateGetSetSymbol(this MacroContext context, SymbolInterpretations interpretation, PCall call, string id, params IAstExpression[] args)
        {
            var sym = new AstGetSetSymbol(context.Invocation.File, context.Invocation.Line,
                                          context.Invocation.Column, call, id, interpretation);
            sym.Arguments.AddRange(args);

            return sym;
        }

        public static AstGetSetSymbol CreateGetSetLocal(this MacroContext context, string id, PCall call = PCall.Get)
        {
            return CreateGetSetSymbol(context, SymbolInterpretations.LocalObjectVariable, call, id);
        }

        public static AstGetSetMemberAccess CreateGetSetMember(this MacroContext context, IAstExpression subject, PCall call, string id, params IAstExpression[] args)
        {
            var mem = new AstGetSetMemberAccess(context.Invocation.File, context.Invocation.Line,
                                                context.Invocation.Column, call, subject, id);

            mem.Arguments.AddRange(args);

            return mem;
        }

        public static AstConstant CreateConstant(this MacroContext context, object constant)
        {
            return new AstConstant(context.Invocation.File, context.Invocation.Line,
                                   context.Invocation.Column, constant);
        }

        public static IAstExpression ToExpression(this PCall call, MacroContext context)
        {
            string member;
            switch (call)
            {
                case PCall.Get:
                    member = "Get";
                    break;
                case PCall.Set:
                    member = "Set";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("call");
            }

            var pcallT = new AstConstantTypeExpression(context.Invocation.File,
                                                       context.Invocation.Line,
                                                       context.Invocation.Column,
                                                       PType.Object[typeof (PCall)].ToString());
            return new AstGetSetStatic(context.Invocation.File, context.Invocation.Line,
                                       context.Invocation.Column, PCall.Get, pcallT, member);
        }

        public static IAstExpression ToExpression(this SymbolInterpretations interpretation, MacroContext context)
        {
            var member = Enum.GetName(typeof (SymbolInterpretations), interpretation);
            var pcallT = new AstConstantTypeExpression(context.Invocation.File,
                                                       context.Invocation.Line,
                                                       context.Invocation.Column,
                                                       PType.Object[typeof(SymbolInterpretations)].ToString());
            return new AstGetSetStatic(context.Invocation.File, context.Invocation.Line,
                                       context.Invocation.Column, PCall.Get, pcallT, member);
        }

        public static bool CallerIsMacro(this MacroContext context)
        {
            return context.Function.IsMacro || context.GetParentFunctions().Any(f => f.IsMacro);
        }

        public static void EstablishMacroContext(this MacroContext context)
        {
            if(!CallerIsMacro(context))
            {
                context.ReportMessage(ParseMessageSeverity.Error, "Cannot establish macro context outside of macro.");
                return;
            }

            if(!context.OuterVariables.Contains(MacroAliases.ContextAlias,Engine.DefaultStringComparer))
                context.RequireOuterVariable(MacroAliases.ContextAlias);
        }
    }
}
