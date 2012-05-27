// Prexonite
// 
// Copyright (c) 2011, Christian Klauser
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without modification, 
//  are permitted provided that the following conditions are met:
// 
//     Redistributions of source code must retain the above copyright notice, 
//          this list of conditions and the following disclaimer.
//     Redistributions in binary form must reproduce the above copyright notice, 
//          this list of conditions and the following disclaimer in the 
//          documentation and/or other materials provided with the distribution.
//     The names of the contributors may be used to endorse or 
//          promote products derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
//  ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED 
//  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
//  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, 
//  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
//  (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, 
//  DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, 
//  WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING 
//  IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System.Linq;
using Prexonite.Types;

namespace Prexonite.Compiler.Ast
{
    public class AstLoopExpression : AstExpr,
                                     IAstHasBlocks,
                                     IAstHasExpressions
    {
        public AstLoopExpression(string file, int line, int column, AstLoop loop)
            : base(file, line, column)
        {
            Loop = loop;
        }

        internal AstLoopExpression(Parser p, AstLoop loop)
            : base(p)
        {
            Loop = loop;
        }

        public AstLoop Loop;
        private string _lstVar;
        private string _tmpVar;
        private bool _useTmpVar;

        #region IAstHasBlocks Members

        public AstBlock[] Blocks
        {
            get { return Loop.Blocks; }
        }

        #region IAstHasExpressions Members

        public AstExpr[] Expressions
        {
            get { return Loop.Expressions; }
        }

        #endregion

        #endregion

        private static bool _tmpIsUsed(AstBlock block)
        {
            //Scanning pass (find out if tmp is used or not)
            for (var i = 0; i < block.Count; i++)
            {
                var ret = block[i] as AstReturn;
                if (ret != null)
                {
                    if ((ret.ReturnVariant == ReturnVariant.Continue && ret.Expression == null)
                        || ret.ReturnVariant == ReturnVariant.Set)
                        return true;
                }

                var hasBlocks = block[i] as IAstHasBlocks;
                if (hasBlocks != null && hasBlocks.Blocks.Any(_tmpIsUsed)) 
                    return true;
            }

            return false;
        }

        private void _transformBlock(AstBlock block)
        {
            for (var i = 0; i < block.Count; i++)
            {
                #region Transformation

                var ret = block[i] as AstReturn;
                if (ret != null)
                {
                    if (ret.ReturnVariant == ReturnVariant.Continue)
                    {
                        if (ret.Expression == null)
                        {
                            //Replace {yield;} by {lst[] = tmp;}
                            AstGetSet addTmpToList =
                                new AstGetSetMemberAccess(
                                    ret.File,
                                    ret.Line,
                                    ret.Column,
                                    PCall.Set,
                                    new AstGetSetSymbol(
                                        ret.File,
                                        ret.Line,
                                        ret.Column,
                                        PCall.Get,
                                        new SymbolEntry(SymbolInterpretations.LocalObjectVariable,
                                            _lstVar, null)),
                                    "");
                            addTmpToList.Arguments.Add(
                                new AstGetSetSymbol(
                                    ret.File,
                                    ret.Line,
                                    ret.Column,
                                    PCall.Get,
                                    new SymbolEntry(SymbolInterpretations.LocalObjectVariable,
                                        _tmpVar, null)));
                            block[i] = addTmpToList;
                        }
                        else
                        {
                            //Replace {yield expr;} by {if($useTmpVar) tmp = expr; lst[] = if($useTmpVar) tmp else expr;}

                            if (_useTmpVar)
                            {
                                var replacement = new AstSubBlock(ret,block,prefix:"yield");
                                var setTmp =
                                    new AstGetSetSymbol(
                                        ret.File,
                                        ret.Line,
                                        ret.Column,
                                        PCall.Set,
                                        new SymbolEntry(SymbolInterpretations.LocalObjectVariable,
                                        _tmpVar, null));
                                setTmp.Arguments.Add(ret.Expression);
                                AstGetSet addExprToList =
                                    new AstGetSetMemberAccess(
                                        ret.File,
                                        ret.Line,
                                        ret.Column,
                                        PCall.Set,
                                        new AstGetSetSymbol(
                                            ret.File,
                                            ret.Line,
                                            ret.Column,
                                            PCall.Get,
                                            new SymbolEntry(SymbolInterpretations.LocalObjectVariable,
                                            _lstVar, null)),
                                        "");
                                addExprToList.Arguments.Add(
                                    new AstGetSetSymbol(
                                        ret.File,
                                        ret.Line,
                                        ret.Column,
                                        PCall.Get,
                                        new SymbolEntry(SymbolInterpretations.LocalObjectVariable,
                                        _tmpVar, null)));

                                replacement.Add(setTmp);
                                replacement.Add(addExprToList);

                                block[i] = replacement;
                            }
                            else
                            {
                                AstGetSet addExprToList =
                                    new AstGetSetMemberAccess(
                                        ret.File,
                                        ret.Line,
                                        ret.Column,
                                        PCall.Set,
                                        new AstGetSetSymbol(
                                            ret.File,
                                            ret.Line,
                                            ret.Column,
                                            PCall.Get,
                                            new SymbolEntry(SymbolInterpretations.LocalObjectVariable,
                                            _lstVar, null)),
                                        "");
                                addExprToList.Arguments.Add(ret.Expression);
                                block[i] = addExprToList;
                            }
                        }
                    }
                    else if (ret.ReturnVariant == ReturnVariant.Set)
                    {
                        //Replace {return = expr;} and {yield = expr;} by {tmp = expr;}.
                        AstGetSet setTmp =
                            new AstGetSetSymbol(
                                ret.File,
                                ret.Line,
                                ret.Column,
                                PCall.Set,
                                new SymbolEntry(SymbolInterpretations.LocalObjectVariable,
                                        _tmpVar, null));
                        setTmp.Arguments.Add(ret.Expression);
                        block[i] = setTmp;
                    }
                }

                #endregion

                #region Recursive Descent

                var hasBlocks = block[i] as IAstHasBlocks;

                if (hasBlocks != null)
                {
                    foreach (var subBlock in hasBlocks.Blocks)
                    {
                        _transformBlock(subBlock);
                    }
                }

                #endregion
            }
        }

        public override bool TryOptimize(CompilerTarget target, out AstExpr expr)
        {
            if (_lstVar != null)
                goto leave;

            //Perform statement to expression transformation
            _lstVar = Loop.Block.CreateLabel("lst");
            _tmpVar = Loop.Block.CreateLabel("tmp");

            foreach (var block in Loop.Blocks)
            {
                if (_tmpIsUsed(block))
                    _useTmpVar = true;
            }

            foreach (var block in Loop.Blocks)
            {
                _transformBlock(block);
            }

            leave: //Optimization occurs during code generation
            expr = null;
            return false;
        }

        protected override void DoEmitCode(CompilerTarget target, StackSemantics stackSemantics)
        {
            if (_lstVar == null)
            {
                AstExpr dummy; //Won't return anything anyway...
                TryOptimize(target, out dummy);
            }

            //Register variables
            target.Function.Variables.Add(_lstVar);
            if (_useTmpVar)
                target.Function.Variables.Add(_tmpVar);

            //Initialize the list
            target.EmitStaticGetCall(this, 0, "List", "Create");
            target.EmitStoreLocal(this, _lstVar);

            //Emit the modified loop
            Loop.EmitEffectCode(target);

            //Return the list
            if(stackSemantics == StackSemantics.Value)
                target.EmitLoadLocal(this, _lstVar);

            //Mark the function as volatile
            //  Using loop expressions with a non-empty stack causes verification errors in CIL implementations because of
            //      - backward branch constraints
            //      - guarded blocks (which require an empty stack on entry an exit)
            //  Possible fix
            //      - automatically export the loop into a separate function/closure
            target.Function.Meta[PFunction.VolatileKey] = true;
            target.Function.Meta[PFunction.DeficiencyKey] = "Uses loop expression";
        }
    }
}