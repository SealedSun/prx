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

using System.Diagnostics;
using Prexonite.Types;
using NoDebug = System.Diagnostics.DebuggerNonUserCodeAttribute;

namespace Prexonite.Compiler.Ast
{
    public class AstWhileLoop : AstLoop
    {
        [DebuggerStepThrough]
        public AstWhileLoop(string file, int line, int column, bool isPrecondition, bool isNegative)
            : base(file, line, column)
        {
            IsPrecondition = isPrecondition;
            IsPositive = isNegative;
            Block = new AstLoopBlock(file, line, column);
        }

        [DebuggerStepThrough]
        public AstWhileLoop(string file, int line, int column, bool isPrecondition)
            : this(file, line, column, isPrecondition, false)
        {
        }

        [DebuggerStepThrough]
        internal AstWhileLoop(Parser p, bool isPrecondition, bool isNegative)
            : this(p.scanner.File, p.t.line, p.t.col, isPrecondition, isNegative)
        {
        }

        [DebuggerStepThrough]
        internal AstWhileLoop(Parser p, bool isPrecondition)
            : this(p, isPrecondition, false)
        {
        }

        public IAstExpression Condition;
        public bool IsPrecondition;
        public bool IsPositive;

        public override IAstExpression[] Expressions
        {
            get { return new[] {Condition}; }
        }

        public bool IsInitialized
        {
            [DebuggerStepThrough]
            get { return Condition != null; }
        }

        protected override void DoEmitCode(CompilerTarget target)
        {
            if (!IsInitialized)
                throw new PrexoniteException("AstWhileLoop requires Condition to be set.");

            //Optimize unary not condition
            OptimizeNode(target, ref Condition);
            var unaryCond = Condition as AstUnaryOperator;
            while (unaryCond != null && unaryCond.Operator == UnaryOperator.LogicalNot)
            {
                Condition = unaryCond.Operand;
                IsPositive = !IsPositive;
                unaryCond = Condition as AstUnaryOperator;
            }

            //Constant conditions
            var conditionIsConstant = false;
            if (Condition is AstConstant)
            {
                var constCond = (AstConstant) Condition;
                PValue condValue;
                if (
                    !constCond.ToPValue(target).TryConvertTo(
                         target.Loader, PType.Bool, out condValue))
                    goto continueFull;
                else if ((bool) condValue.Value == IsPositive)
                    conditionIsConstant = true;
                else
                {
                    //Condition is always false
                    if (!IsPrecondition) //If do-while, emit the body without loop code
                    {
                        target.BeginBlock(Block);
                        Block.EmitCode(target);
                        target.EndBlock();
                    }
                    return;
                }
            }
            continueFull:

            target.BeginBlock(Block);
            if (!Block.IsEmpty) //Body exists -> complete loop code?
            {
                if (conditionIsConstant) //Infinite, hopefully user managed, loop ->
                {
                    target.EmitLabel(this, Block.ContinueLabel);
                    target.EmitLabel(this, Block.BeginLabel);
                    Block.EmitCode(target);
                    target.EmitJump(this, Block.ContinueLabel);
                }
                else
                {
                    if (IsPrecondition)
                        target.EmitJump(this, Block.ContinueLabel);

                    target.EmitLabel(this, Block.BeginLabel);
                    Block.EmitCode(target);

                    _emitCondition(target);
                    
                }
            }
            else //Body does not exist -> Condition loop
            {
                target.EmitLabel(this, Block.BeginLabel);
                _emitCondition(target);
            }

            target.EmitLabel(this, Block.BreakLabel);
            target.EndBlock();
        }

        private void _emitCondition(CompilerTarget target)
        {
            target.EmitLabel(this, Block.ContinueLabel);
            AstLazyLogical.EmitJumpCondition(target, Condition, Block.BeginLabel, IsPositive);
        }
    }
}