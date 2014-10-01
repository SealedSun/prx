﻿// Prexonite
// 
// Copyright (c) 2014, Christian Klauser
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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Prexonite.Internal;

namespace Prexonite.Modular
{

    /// <summary>
    /// A Prexonite module is a named container for  Prexonite functions and variable declarations.
    /// </summary>
    [DebuggerDisplay("module {Name}")]
    public abstract class Module : IHasMetaTable, IMetaFilter
    {
        public const string NameKey = Application.NameKey;
        public const string ReferencesKey = "references";
        public const string NoStandardLibraryKey = "nostdlib";

        public abstract ModuleName Name { get; }

        public abstract MetaTable Meta { get; }

        public abstract FunctionTable Functions { get; }

        public abstract VariableTable Variables { get; }

        public abstract CentralCache Cache { get; internal set; }

        #region IMetaFilter Members

        [DebuggerStepThrough]
        string IMetaFilter.GetTransform(string key)
        {
            return GetTransform(key);
        }

        protected virtual string GetTransform(string key)
        {
            if (Engine.StringsAreEqual(key, Application.NameKey))
                return Application.IdKey;
            else if (Engine.StringsAreEqual(key, "imports"))
                return Application.ImportKey;
            else
                return key;
        }

        [DebuggerStepThrough]
        KeyValuePair<string, MetaEntry>? IMetaFilter.SetTransform(
            KeyValuePair<string, MetaEntry> item)
        {
            return SetTransform(item);
        }

        protected virtual KeyValuePair<string, MetaEntry>? SetTransform(KeyValuePair<string, MetaEntry> item)
        {
            if (Engine.StringsAreEqual(item.Key, Application.NameKey))
                item = new KeyValuePair<string, MetaEntry>(Application.IdKey, item.Value);
            else if (Engine.StringsAreEqual(item.Key, "imports"))
                item = new KeyValuePair<string, MetaEntry>(Application.ImportKey, item.Value);
            return item;
        }
        
        #endregion

        public static Module Create(ModuleName moduleName)
        {
            var moduleImpl = new ModuleImpl(moduleName);
            Debug.Assert(moduleImpl.Functions.Contains(Application.InitializationId));
            return moduleImpl;
        }

        public FunctionDeclaration CreateFunction(string id)
        {
            if (Functions.Contains(id))
                throw new PrexoniteException(
                    string.Format(
                        "Cannot declare function with physical ID '{0}'. A function with that ID already exists in {1}.",
                        id, Name));
            var decl = FunctionDeclaration._Create(id,this);
            Functions.Add(decl);
            return decl;
        }
    }

    public class FunctionTable : System.Collections.ObjectModel.KeyedCollection<string, FunctionDeclaration>
    {
        public FunctionTable() : base(StringComparer.InvariantCultureIgnoreCase)
        {
            _idChangingHandler = _onIdChanging;
        }

        protected override string GetKeyForItem(FunctionDeclaration item)
        {
            return item.Id;
        }

        private readonly EventHandler<FunctionIdChangingEventArgs> _idChangingHandler;

        [EditorBrowsable(EditorBrowsableState.Never)]
        private void _onIdChanging(object o, FunctionIdChangingEventArgs args)
        {
            var sender = (FunctionDeclaration) o;
            if (Contains(sender))
            {
                ChangeItemKey(sender, args.NewId);
            }
            else
            {
                Debug.Assert(false,
                    string.Format(
                        "Function table is still registered to function declaration {0} even though it is no longer in the table.",
                        sender));
            }
        }

        protected override void ClearItems()
        {
            foreach (var funDecl in this)
                funDecl.IdChanging -= _idChangingHandler;
            base.ClearItems();
        }

        protected override void InsertItem(int index, FunctionDeclaration item)
        {
            item.IdChanging += _idChangingHandler;
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            this[index].IdChanging -= _idChangingHandler;
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, FunctionDeclaration item)
        {
            this[index].IdChanging -= _idChangingHandler;
            base.SetItem(index, item);
            this[index].IdChanging += _idChangingHandler;
        }

        /// <summary>
        /// Returns the function declaration with the specified id, if it exists in the table.
        /// </summary>
        /// <param name="id">The physical id of the function declaration to return.</param>
        /// <param name="declaration">On success, will contain the function declaration. Undefined on failure.</param>
        /// <returns>True on success; false otherwise</returns>
        public bool TryGetFunction(string id, out FunctionDeclaration declaration)
        {
            if(Contains(id))
            {
                declaration=this[id];
                return true;
            }
            else
            {
                declaration = null;
                return false;
            }
        }

        public void Store(TextWriter writer)
        {
            foreach (var decl in this)
                decl.Store(writer);
        }
    }

    class ModuleImpl : Module
    {
        private CentralCache _cache = CentralCache.Create();

        public ModuleImpl(ModuleName name)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            
            _name = name;
            var m = MetaTable.Create(this);
            m[Application.IdKey] = name.ToMetaEntry();
            _meta = m;
            _meta[Application.EntryKey] = Application.DefaultEntryFunction;
            _meta[Application.ImportKey] = Application.DefaultImport;

            _functions.Add(FunctionDeclaration._Create(Application.InitializationId,this));
        }

        public override CentralCache Cache
        {
            get { return _cache; }
            internal set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                
                _cache = value;
            }
        }

        private readonly ModuleName _name;
        private readonly FunctionTable _functions = new FunctionTable();
        private readonly VariableTable _variables = new VariableTable();
        private readonly MetaTable _meta; // must be assigned in the constructor

        public override ModuleName Name
        {
            get { return _name; }
        }

        public override MetaTable Meta
        {
            get { return _meta; }
        }

        public override FunctionTable Functions
        {
            get { return _functions; }
        }

        public override VariableTable Variables
        {
            get { return _variables; }
        }

        #region IMetaFilter

        protected override string GetTransform(string key)
        {
            if (_meta == null)
                return key;
            else
                return base.GetTransform(key);
        }

        protected override KeyValuePair<string, MetaEntry>? SetTransform(KeyValuePair<string, MetaEntry> item)
        {
            if (_meta == null)
                return item;
            else
                return base.SetTransform(item);
        }

        #endregion
    }
}
