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
using System.Diagnostics;
using Prexonite.Types;

namespace Prexonite.Compiler.Ast
{
    public abstract class AstGetSetImplBase : AstGetSet
    {
        private readonly ArgumentsProxy _proxy;

        public override ArgumentsProxy Arguments
        {
            [DebuggerNonUserCode]
            get { return _proxy; }
        }

        #region IAstHasExpressions Members

        private PCall _call;

        /// <summary>
        ///     <para>Indicates whether this node uses get or set syntax</para>
        ///     <para>(set syntax involves an equal sign (=); get syntax does not)</para>
        /// </summary>
        public override PCall Call
        {
            get { return _call; }
            set { _call = value; }
        }

        #endregion

        protected AstGetSetImplBase(string file, int line, int column, PCall call)
            : this(new SourcePosition(file,line,column), call)
        {
        }

        protected AstGetSetImplBase(ISourcePosition position, PCall call)
            : base(position)
        {
            _call = call;
            _proxy = new ArgumentsProxy(new List<AstExpr>());
        }

        internal AstGetSetImplBase(Parser p, PCall call)
            : this(p.scanner.File, p.t.line, p.t.col, call)
        {
        }
    }
}