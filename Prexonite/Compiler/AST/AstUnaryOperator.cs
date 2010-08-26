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
using Prexonite.Types;

namespace Prexonite.Compiler.Ast
{
    public class AstUnaryOperator : AstNode,
                                    IAstEffect,
                                    IAstHasExpressions
    {
        public IAstExpression Operand;
        public UnaryOperator Operator;

        public AstUnaryOperator(
            string file, int line, int column, UnaryOperator op, IAstExpression operand)
            : base(file, line, column)
        {
            if (operand == null)
                throw new ArgumentNullException("operand");
            Operator = op;
            Operand = operand;
        }

        internal AstUnaryOperator(Parser p, UnaryOperator op, IAstExpression operand)
            : this(p.scanner.File, p.t.line, p.t.col, op, operand)
        {
        }

        #region IAstHasExpressions Members

        public IAstExpression[] Expressions
        {
            get { return new[] {Operand}; }
        }

        #endregion

        #region IAstExpression Members

        public bool TryOptimize(CompilerTarget target, out IAstExpression expr)
        {
            expr = null;
            OptimizeNode(target, ref Operand);
            if (Operand is AstConstant)
            {
                var constOperand = (AstConstant) Operand;
                var valueOperand = constOperand.ToPValue(target);
                PValue result;
                switch (Operator)
                {
                    case UnaryOperator.UnaryNegation:
                        if (valueOperand.UnaryNegation(target.Loader, out result))
                            goto emitConstant;
                        break;
                    case UnaryOperator.LogicalNot:
                        if (valueOperand.LogicalNot(target.Loader, out result))
                            goto emitConstant;
                        break;
                    case UnaryOperator.OnesComplement:
                        if (valueOperand.OnesComplement(target.Loader, out result))
                            goto emitConstant;
                        break;
                    case UnaryOperator.PreIncrement:
                        if (valueOperand.Increment(target.Loader, out result))
                            goto emitConstant;
                        break;
                    case UnaryOperator.PreDecrement:
                        if (valueOperand.Decrement(target.Loader, out result))
                            goto emitConstant;
                        break;
                    case UnaryOperator.PostIncrement:
                    case UnaryOperator.PostDecrement:
                        //No optimization allowed/needed here
                        break;
                }
                goto emitFull;

                emitConstant:
                return AstConstant.TryCreateConstant(target, this, result, out expr);
                emitFull:
                return false;
            }

            //Try other optimizations
            switch (Operator)
            {
                case UnaryOperator.UnaryNegation:
                case UnaryOperator.LogicalNot:
                case UnaryOperator.OnesComplement:
                    var doubleNegation = Operand as AstUnaryOperator;
                    if (doubleNegation != null && doubleNegation.Operator == Operator)
                    {
                        expr = doubleNegation.Operand;
                        return true;
                    }
                    break;
                case UnaryOperator.PreIncrement:
                case UnaryOperator.PreDecrement:
                case UnaryOperator.PostIncrement:
                case UnaryOperator.PostDecrement:
                    //No optimization
                    break;
            }
            expr = null;
            return false;
        }

        #endregion

        void IAstEffect.DoEmitEffectCode(CompilerTarget target)
        {
            var symbol = Operand as AstGetSetSymbol;
            var isVariable = symbol != null && symbol.IsObjectVariable;
            var complex = Operand as AstGetSet;
            var isAssignable = complex != null;
            switch (Operator)
            {
                case UnaryOperator.PreIncrement:
                case UnaryOperator.PostIncrement:
                case UnaryOperator.PreDecrement:
                case UnaryOperator.PostDecrement:
                    if (isVariable) //The easy way
                    {
                        OpCode opc;
                        if (Operator == UnaryOperator.PostIncrement ||
                            Operator == UnaryOperator.PreIncrement)
                            if (symbol.Interpretation == SymbolInterpretations.GlobalObjectVariable)
                                opc = OpCode.incglob;
                            else
                                opc = OpCode.incloc;
                        else if (symbol.Interpretation == SymbolInterpretations.GlobalObjectVariable)
                            opc = OpCode.decglob;
                        else
                            opc = OpCode.decloc;
                        target.Emit(this, opc, symbol.Id);
                    }
                    else if (isAssignable)
                    {
                        //The get/set fallback
                        complex = complex.GetCopy();
                        var assignment = new AstModifyingAssignment(
                            complex.File, complex.Line, complex.Column, Operator ==
                                                                        UnaryOperator.PostIncrement ||
                                                                        Operator == UnaryOperator.PreIncrement
                                                                            ?
                                                                                BinaryOperator.Addition
                                                                            : BinaryOperator.Subtraction, complex);
                        if (complex.Call == PCall.Get)
                            complex.Arguments.Add(new AstConstant(File, Line, Column, 1));
                        else
                            complex.Arguments[complex.Arguments.Count - 1] =
                                new AstConstant(File, Line, Column, 1);
                        complex.Call = PCall.Set;
                        assignment.EmitCode(target);
                    }
                    else
                        throw new PrexoniteException(
                            "Node of type " + Operand.GetType() +
                            " does not support increment/decrement operators.");
                    break;
                case UnaryOperator.UnaryNegation:
                case UnaryOperator.LogicalNot:
                case UnaryOperator.OnesComplement:
                default:
                    break; //No effect
            }
        }

        protected override void DoEmitCode(CompilerTarget target)
        {
            switch (Operator)
            {
                case UnaryOperator.UnaryNegation:
                    Operand.EmitCode(target);
                    target.Emit(this, OpCode.neg);
                    break;
                case UnaryOperator.LogicalNot:
                    Operand.EmitCode(target);
                    target.Emit(this, OpCode.not);
                    break;
                case UnaryOperator.OnesComplement:
                    Operand.EmitCode(target);
                    target.Emit(this, OpCode.neg);
                    break;
                case UnaryOperator.PreDecrement:
                case UnaryOperator.PreIncrement:
                    ((IAstEffect)this).DoEmitEffectCode(target);
                    Operand.EmitCode(target);
                    break;
                case UnaryOperator.PostDecrement:
                case UnaryOperator.PostIncrement:
                    Operand.EmitCode(target);
                    ((IAstEffect)this).DoEmitEffectCode(target);
                    break;
            }
        }
    }

    public enum UnaryOperator
    {
        None,
        UnaryNegation,
        LogicalNot,
        OnesComplement,
        PreIncrement,
        PreDecrement,
        PostIncrement,
        PostDecrement
    }
}