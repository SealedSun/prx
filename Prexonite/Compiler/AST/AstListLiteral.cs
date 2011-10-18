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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Prexonite.Types;

namespace Prexonite.Compiler.Ast
{
    public class AstListLiteral : AstNode,
                                  IAstExpression,
                                  IAstHasExpressions,
                                  IAstPartiallyApplicable
    {
        public List<IAstExpression> Elements = new List<IAstExpression>();

        internal AstListLiteral(Parser p)
            : base(p)
        {
        }

        public AstListLiteral(string file, int line, int column)
            : base(file, line, column)
        {
        }

        #region IAstHasExpressions Members

        public IAstExpression[] Expressions
        {
            get { return Elements.ToArray(); }
        }

        #endregion

        #region IAstExpression Members

        public bool TryOptimize(CompilerTarget target, out IAstExpression expr)
        {
            foreach (var arg in Elements.ToArray())
            {
                if (arg == null)
                    throw new PrexoniteException(
                        "Invalid (null) argument in ListLiteral node (" + ToString() +
                            ") detected at position " + Elements.IndexOf(arg) + ".");
                var oArg = _GetOptimizedNode(target, arg);
                if (!ReferenceEquals(oArg, arg))
                {
                    var idx = Elements.IndexOf(arg);
                    Elements.Insert(idx, oArg);
                    Elements.RemoveAt(idx + 1);
                }
            }
            expr = null;
            return false;
        }

        #endregion

        protected override void DoEmitCode(CompilerTarget target)
        {
            var call = new AstGetSetSymbol(
                File, Line, Column, PCall.Get, Engine.ListAlias, SymbolInterpretations.Command);
            call.Arguments.AddRange(Elements);
            call.EmitCode(target);
        }

        #region Implementation of IAstPartiallyApplicable

        public void DoEmitPartialApplicationCode(CompilerTarget target)
        {
            DoEmitCode(target);
            //Code is the same. Partial application is handled by AstGetSetSymbol
        }

        public override bool CheckForPlaceholders()
        {
            return base.CheckForPlaceholders() || Elements.Any(AstPartiallyApplicable.IsPlaceholder);
        }

        #endregion

        public override string ToString()
        {
            const int limit = 20;
            var end = Elements.Count == limit + 1 ? limit + 1 : Math.Min(limit, Elements.Count);
            var sb = new StringBuilder("[ ", end*15);
            var i = 0;
            for (; i < end; i++)
            {
                sb.Append(Elements[i]);
                if (i + 1 < end)
                    sb.Append(", ");
            }

            if (i < Elements.Count)
            {
                sb.AppendFormat(", ... �{0}� ..., {1} ]", Elements.Count - limit,
                    Elements[Elements.Count - 1]);
            }
            else
            {
                sb.Append(" ]");
            }

            return sb.ToString();
        }
    }
}