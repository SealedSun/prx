﻿// Prexonite
// 
// Copyright (c) 2013, Christian Klauser
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
using System.Linq;
using JetBrains.Annotations;
using Prexonite.Compiler.Ast;
using Prexonite.Compiler.Symbolic;
using Prexonite.Types;

namespace Prexonite.Compiler.Macro
{
    /// <summary>
    ///     Provides macros with access to Prexonite and compiler internals.
    /// </summary>
    [PublicAPI]
    public class MacroContext
    {
        #region Representation

        [NotNull]
        private readonly AstGetSet _invocation;

        private readonly bool _isJustEffect;

        [NotNull]
        private readonly MacroSession _session;

        [NotNull]
        private readonly AstBlock _block;

        private readonly bool _isPartialApplication;
        
        /// <summary>
        /// In order to ensure that the macro doesn't pop too many blocks, 
        /// need to remember the current block when the context was created.
        /// That block can not be popped via the context.
        /// </summary>
        private readonly AstBlock _sentinelBlock;

        #endregion

        /// <summary>
        ///     Creates a new macro context in the specified macro session.
        /// </summary>
        /// <param name = "session">The macro expansion session.</param>
        /// <param name = "invocation">The node that is being expanded.</param>
        /// <param name = "isJustEffect">Whether the nodes return value will be discarded by the surrounding program.</param>
        internal MacroContext(MacroSession session, AstGetSet invocation, bool isJustEffect)
        {
            if (session == null)
                throw new ArgumentNullException("session");
            if (invocation == null)
                throw new ArgumentNullException("invocation");
            _isJustEffect = isJustEffect;
            _invocation = invocation;
            _session = session;
            _block = new AstScopedBlock(invocation.Position, session.CurrentBlock);
            _isPartialApplication = _invocation.Arguments.Any(AstPartiallyApplicable.IsPlaceholder);
            _sentinelBlock = session.Target.CurrentBlock;
        }

        #region Accessors 

        [PublicAPI]
        public bool SuppressDefaultExpression { get; set; }

        /// <summary>
        ///     The block of code generated by the macro.
        /// </summary>
        [PublicAPI]
        public AstBlock Block
        {
            [DebuggerStepThrough]
            get { return _block; }
        }

        /// <summary>
        ///     Indicates whether the occurance of the macro is a partial application of the macro.
        /// </summary>
        [PublicAPI]
        public bool IsPartialApplication
        {
            [DebuggerStepThrough]
            get { return _isPartialApplication; }
        }

        /// <summary>
        ///     The node that is being expanded.
        /// </summary>
        [PublicAPI]
        public AstGetSet Invocation
        {
            get { return _invocation; }
        }

        /// <summary>
        ///     Determines whether the macro is used as a get or as a set call.
        /// </summary>
        [PublicAPI]
        public PCall Call
        {
            get { return Invocation.Call; }
        }

        /// <summary>
        ///     Indicates whether the macros return value should produce a return value.
        /// </summary>
        [PublicAPI]
        public bool IsJustEffect
        {
            get { return _isJustEffect; }
        }

        /// <summary>
        ///     Provides access to a copy of the loader options currently in effect.
        /// </summary>
        [PublicAPI]
        public LoaderOptions LoaderOptions
        {
            get { return _session.LoaderOptions; }
        }

        /// <summary>
        ///     Returns the loop block (reacting to break, continue) that is currently active. Can be null.
        /// </summary>
        [PublicAPI]
        public ILoopBlock CurrentLoopBlock
        {
            get { return _session.CurrentLoopBlock; }
        }

        /// <summary>
        /// Returns the closest lexically scoped block.
        /// </summary>
        [PublicAPI]
        public AstBlock CurrentBlock
        {
            get { return _session.CurrentBlock; }
        }

        [PublicAPI]
        public void PushBlock([NotNull] AstScopedBlock block)
        {
            if (block == null)
                throw new ArgumentNullException("block");
            if(!ReferenceEquals(block.LexicalScope, CurrentBlock))
                throw new PrexoniteException("The block pushed by the macro is not a direct lexical child of the currently enclosing scope.");
            _session.Target.BeginBlock(block);
        }

        [PublicAPI, NotNull]
        public AstScopedBlock PopBlock()
        {
            if(ReferenceEquals(_session.Target.CurrentBlock,_sentinelBlock))
                throw new PrexoniteException("A macro cannot pop lexical scopes that it didn't push itself.");
            return _session.Target.EndBlock();
        }

        /// <summary>
        ///     Provides read-only access to the local symbol table.
        /// </summary>
        [PublicAPI]
        [Obsolete("LocalSymbols is obsolete. Local symbol lookup should happen via the AST Block.")]
        public SymbolStore LocalSymbols
        {
            get { return CurrentBlock.Symbols; }
        }

        /// <summary>
        ///     Provides read-only access to the global symbol table.
        /// </summary>
        [PublicAPI]
        public SymbolStore GlobalSymbols
        {
            get { return _session.GlobalSymbols; }
        }

        /// <summary>
        ///     Provides read-only access to the set of variables shared from outer functions.
        /// </summary>
        [PublicAPI]
        public ReadOnlyCollectionView<string> OuterVariables
        {
            get { return _session.OuterVariables; }
        }

        /// <summary>
        ///     A reference to the application being compiled.
        /// </summary>
        [PublicAPI]
        public Application Application
        {
            get { return _session.Target.Loader.ParentApplication; }
        }

        /// <summary>
        ///     A reference to the function being compiled.
        /// </summary>
        [PublicAPI]
        public PFunction Function
        {
            get { return _session.Target.Function; }
        }

        /// <summary>
        ///     Returns the functions proper parent functions in reverse order (closest parent first).
        /// </summary>
        [PublicAPI]
        public IEnumerable<PFunction> GetParentFunctions()
        {
            var target = _session.Target.ParentTarget;
            while (target != null)
            {
                yield return target.Function;
                target = target.ParentTarget;
            }
        }

        #endregion

        #region Compiler interaction
        
        [PublicAPI]
        public IAstFactory Factory { get { return _session.Factory; } }

        /// <summary>
        ///     Allocates a temporary variable for this macro expansion session.
        /// </summary>
        /// <returns>The (physical) id of a free temporary variable.</returns>
        /// <remarks>
        ///     If a temporary variable is not freed during a macro expansion session, 
        ///     it will no longer be considered a temporary variable and cannot be freed in 
        ///     subsequent expansions
        /// </remarks>
        [PublicAPI]
        public string AllocateTemporaryVariable()
        {
            return _session.AllocateTemporaryVariable();
        }

        /// <summary>
        ///     Marks the temporary variable for freeing at the end of this expansion session.
        /// </summary>
        /// <param name = "temporaryVariable">The temporary variable to be freed.</param>
        [PublicAPI]
        public void FreeTemporaryVariable(string temporaryVariable)
        {
            _session.FreeTemporaryVariable(temporaryVariable);
        }

        /// <summary>
        ///     Reports a compiler message (error, warning, info).
        /// </summary>
        /// <param name = "severity">The message severity (error, warning, info)</param>
        /// <param name = "message">The actual message (human-readable)</param>
        /// <param name = "position">The location in the code associated with the message.</param>
        /// <remarks>
        ///     Issuing an error message does not automatically abort execution of the macro.
        /// </remarks>
        [PublicAPI,Obsolete("Use ReportMessage(Message) instead. Always pass message classes, especially with warnings and infos.")]
        public void ReportMessage(MessageSeverity severity, string message,
            ISourcePosition position = null)
        {
            position = position ?? _invocation.Position;
            _session.Target.Loader.ReportMessage(Message.Create(severity, message, position,null));
        }

        /// <summary>
        /// Reports a compiler message (error, warning, info).
        /// </summary>
        /// <param name="message">The message to be reported.</param>
        /// <remarks>
        /// <para>Issuing an error message does not automatically abort execution of the macro.</para>
        /// <para>Messages should always have a message class. Especially warings and infos (that way, the user can filter undesired warnings/infos)</para>
        /// </remarks>
        [PublicAPI]
        public void ReportMessage([NotNull] Message message)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            _session.Target.Loader.ReportMessage(message);
        }

        /// <summary>
        ///     Requires that a variable from an outer function scope is shared with the function currently compiled.
        /// </summary>
        /// <param name = "variable">The physical name of the variable to request.</param>
        [PublicAPI]
        public void RequireOuterVariable(string variable)
        {
            _session.Target.RequireOuterVariable(variable);
        }

        /// <summary>
        ///     Attempts to optimize the supplied node. Returns the original node on failure, and the optimized node on success.
        /// </summary>
        /// <param name = "node">The node to be optimized.</param>
        /// <returns>The optimized node on success, the old node on failure. Never null.</returns>
        [PublicAPI]
        public AstNode GetOptimizedNode(AstNode node)
        {
            if (node == null)
                throw new ArgumentNullException("node");
            var expr = node as AstExpr;
            if (expr == null || !expr.TryOptimize(_session.Target, out expr))
                return node;
            else
                return expr;
        }

        /// <summary>
        ///     Stores an object in the macro session. It can later be retrieved via <see cref = "RetrieveFromTransport" />.
        /// </summary>
        /// <param name = "obj">The object to be stored.</param>
        /// <returns>The id with which to retrieve the object later.</returns>
        [PublicAPI]
        public int StoreForTransport(PValue obj)
        {
            return _session.StoreForTransport(obj);
        }

        /// <summary>
        ///     Returns an object previously stored via <see cref = "StoreForTransport" />.
        /// </summary>
        /// <param name = "id">The id as returned by <see cref = "StoreForTransport" /></param>
        /// <returns>The obejct stored before.</returns>
        [PublicAPI]
        public PValue RetrieveFromTransport(int id)
        {
            return _session.RetrieveFromTransport(id);
        }

        #endregion
    }
}