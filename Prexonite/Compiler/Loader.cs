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

#if Compress
using System.IO.Compression;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Prexonite.Commands;
using Prexonite.Compiler.Ast;
using Prexonite.Helper;
using Prexonite.Types;
using NoDebug = System.Diagnostics.DebuggerNonUserCodeAttribute;

namespace Prexonite.Compiler
{
    public class Loader : StackContext
    {
        #region Static

        #endregion

        #region Construction

        [DebuggerStepThrough]
        public Loader(Engine parentEngine, Application targetApplication)
            : this(new LoaderOptions(parentEngine, targetApplication))
        {
        }

        [DebuggerStepThrough]
        public Loader(LoaderOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            _options = options;

            _symbols = new SymbolTable<SymbolEntry>();

            _functionTargets = new SymbolTable<CompilerTarget>();
            _functionTargetsIterator = new FunctionTargetsIterator(this);

            CreateFunctionTarget(
                ParentApplication._InitializationFunction, new AstBlock("~NoFile", -1, -1));

            if (options.RegisterCommands)
                RegisterExistingCommands();

            _compilerHooksIterator = new CompilerHooksIterator(this);
            _customResolversProxy = new CustomResolversIterator(_customResolvers);

            //Build commands
            _initializeBuildCommands();
        }


        public void RegisterExistingCommands()
        {
            foreach (var kvp in ParentEngine.Commands)
                Symbols.Add(kvp.Key, new SymbolEntry(SymbolInterpretations.Command, kvp.Key));
        }

        #endregion

        #region Options

        private readonly LoaderOptions _options;

        public LoaderOptions Options
        {
            [DebuggerStepThrough]
            get { return _options; }
        }

        #endregion

        #region Symbol Table

        private readonly SymbolTable<SymbolEntry> _symbols;

        public SymbolTable<SymbolEntry> Symbols
        {
            /*[DebuggerStepThrough]*/
            get { return _symbols; }
        }

        #endregion

        #region Function Symbol Tables

        private readonly SymbolTable<CompilerTarget> _functionTargets;
        private readonly FunctionTargetsIterator _functionTargetsIterator;

        public FunctionTargetsIterator FunctionTargets
        {
            [DebuggerStepThrough]
            get { return _functionTargetsIterator; }
        }

        [DebuggerStepThrough]
        public sealed class FunctionTargetsIterator
        {
            private readonly Loader _outer;

            internal FunctionTargetsIterator(Loader outer)
            {
                _outer = outer;
            }

            public int Count
            {
                get { return _outer._functionTargets.Count; }
            }

            public CompilerTarget this[string key]
            {
                get { return _outer._functionTargets[key]; }
            }

            public CompilerTarget this[PFunction key]
            {
                get { return _outer._functionTargets[key.Id]; }
            }

            public void Remove(CompilerTarget target)
            {
                var funcId = target.Function.Id;
                if (_outer._functionTargets.ContainsKey(funcId))
                    _outer._functionTargets.Remove(funcId);
            }
        }

        [DebuggerStepThrough]
        public CompilerTarget CreateFunctionTarget(PFunction func, AstBlock block)
        {
            if (func == null)
                throw new ArgumentNullException("func");

            var target = new CompilerTarget(this, func, block);
            if (_functionTargets.ContainsKey(func.Id) &&
                (!ParentApplication.Meta.GetDefault(Application.AllowOverridingKey, true).Switch))
                Errors.Add(
                    string.Format(
                        "The application {0} does not allow overriding of function {1}.", ParentApplication.Id, func.Id));

            //The function target is added nontheless in order not to confuse the compiler
            _functionTargets[func.Id] = target;

            return target;
        }

        #endregion

        #region Compiler Hooks

        private readonly CompilerHooksIterator _compilerHooksIterator;

        public CompilerHooksIterator CompilerHooks
        {
            [DebuggerStepThrough]
            get { return _compilerHooksIterator; }
        }

        private readonly List<CompilerHook> _compilerHooks = new List<CompilerHook>();

        [DebuggerStepThrough]
        public class CompilerHooksIterator : ICollection<CompilerHook>
        {
            private readonly List<CompilerHook> _lst;

            internal CompilerHooksIterator(Loader outer)
            {
                _lst = outer._compilerHooks;
            }

            #region ICollection<CompilerHook> Members

            ///<summary>
            ///Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
            ///</summary>
            ///
            ///<param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1"></see>.</param>
            ///<exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"></see> is read-only.</exception>
            public void Add(CompilerHook item)
            {
                if (item == null)
                    throw new ArgumentNullException("item");
                _lst.Add(item);
            }

            /// <summary>
            /// Adds a managed transformation to the collection.
            /// </summary>
            /// <param name="transformation">A managed transformation.</param>
            public void Add(AstTransformation transformation)
            {
                _lst.Add(new CompilerHook(transformation));
            }

            /// <summary>
            /// Adds an interpreted transformation to the collection.
            /// </summary>
            /// <param name="transformation">An interpreted transformation.</param>
            public void Add(PValue transformation)
            {
                if (transformation.Type.ToBuiltIn() == PType.BuiltIn.Object &&
                    transformation.Value is AstTransformation)
                    _lst.Add(new CompilerHook((AstTransformation) transformation.Value));
                else
                    _lst.Add(new CompilerHook(transformation));
            }

            ///<summary>
            ///Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
            ///</summary>
            ///
            ///<exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"></see> is read-only. </exception>
            public void Clear()
            {
                _lst.Clear();
            }

            ///<summary>
            ///Determines whether the <see cref="T:System.Collections.Generic.ICollection`1"></see> contains a specific value.
            ///</summary>
            ///
            ///<returns>
            ///true if item is found in the <see cref="T:System.Collections.Generic.ICollection`1"></see>; otherwise, false.
            ///</returns>
            ///
            ///<param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1"></see>.</param>
            public bool Contains(CompilerHook item)
            {
                if (item == null)
                    return false;
                return _lst.Contains(item);
            }

            ///<summary>
            ///Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1"></see> to an <see cref="T:System.Array"></see>, starting at a particular <see cref="T:System.Array"></see> index.
            ///</summary>
            ///
            ///<param name="array">The one-dimensional <see cref="T:System.Array"></see> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1"></see>. The <see cref="T:System.Array"></see> must have zero-based indexing.</param>
            ///<param name="arrayIndex">The zero-based index in array at which copying begins.</param>
            ///<exception cref="T:System.ArgumentOutOfRangeException">arrayIndex is less than 0.</exception>
            ///<exception cref="T:System.ArgumentNullException">array is null.</exception>
            ///<exception cref="T:System.ArgumentException">array is multidimensional.-or-arrayIndex is equal to or greater than the length of array.-or-The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1"></see> is greater than the available space from arrayIndex to the end of the destination array.-or-Type T cannot be cast automatically to the type of the destination array.</exception>
            public void CopyTo(CompilerHook[] array, int arrayIndex)
            {
                if (array == null)
                    throw new ArgumentNullException("array");
                _lst.CopyTo(array, arrayIndex);
            }

            ///<summary>
            ///Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
            ///</summary>
            ///
            ///<returns>
            ///true if item was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1"></see>; otherwise, false. This method also returns false if item is not found in the original <see cref="T:System.Collections.Generic.ICollection`1"></see>.
            ///</returns>
            ///
            ///<param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1"></see>.</param>
            ///<exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1"></see> is read-only.</exception>
            public bool Remove(CompilerHook item)
            {
                if (item == null)
                    return false;
                else
                    return _lst.Remove(item);
            }

            ///<summary>
            ///Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
            ///</summary>
            ///
            ///<returns>
            ///The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1"></see>.
            ///</returns>
            ///
            public int Count
            {
                get { return _lst.Count; }
            }

            ///<summary>
            ///Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1"></see> is read-only.
            ///</summary>
            ///
            ///<returns>
            ///true if the <see cref="T:System.Collections.Generic.ICollection`1"></see> is read-only; otherwise, false.
            ///</returns>
            ///
            public bool IsReadOnly
            {
                get { return ((ICollection<CompilerHook>) _lst).IsReadOnly; }
            }

            #endregion

            #region IEnumerable<CompilerHook> Members

            ///<summary>
            ///Returns an enumerator that iterates through the collection.
            ///</summary>
            ///
            ///<returns>
            ///A <see cref="T:System.Collections.Generic.IEnumerator`1"></see> that can be used to iterate through the collection.
            ///</returns>
            ///<filterpriority>1</filterpriority>
            IEnumerator<CompilerHook> IEnumerable<CompilerHook>.GetEnumerator()
            {
                return _lst.GetEnumerator();
            }

            #endregion

            #region IEnumerable Members

            ///<summary>
            ///Returns an enumerator that iterates through a collection.
            ///</summary>
            ///
            ///<returns>
            ///An <see cref="T:System.Collections.IEnumerator"></see> object that can be used to iterate through the collection.
            ///</returns>
            ///<filterpriority>2</filterpriority>
            public IEnumerator GetEnumerator()
            {
                return _lst.GetEnumerator();
            }

            #endregion
        }

        #endregion

        #region Symbol resolving

        private readonly List<CustomResolver> _customResolvers = new List<CustomResolver>();
        private readonly CustomResolversIterator _customResolversProxy;

        public CustomResolversIterator CustomResolvers
        {
            get { return _customResolversProxy; }
        }

        public class CustomResolversIterator : ICollection<CustomResolver>
        {
            private readonly List<CustomResolver> _resolvers;

            internal CustomResolversIterator(List<CustomResolver> outer)
            {
                _resolvers = outer;
            }

            public int Length
            {
                get { return _resolvers.Count; }
            }

            public CustomResolver this[int index]
            {
                get { return _resolvers[index]; }
            }

            public IEnumerator<CustomResolver> GetEnumerator()
            {
                return _resolvers.GetEnumerator();
            }

            #region Implementation of IEnumerable

            /// <summary>
            /// Returns an enumerator that iterates through a collection.
            /// </summary>
            /// <returns>
            /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.
            /// </returns>
            /// <filterpriority>2</filterpriority>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            #endregion

            #region Implementation of ICollection<CustomResolver>

            /// <summary>
            /// Adds an item to the <see cref="T:System.Collections.Generic.ICollection`1" />.
            /// </summary>
            /// <param name="item">The object to add to the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
            /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
            public void Add(CustomResolver item)
            {
                if (item == null)
                    throw new ArgumentNullException("item");
                _resolvers.Add(item);
            }

            /// <summary>
            /// Removes all items from the <see cref="T:System.Collections.Generic.ICollection`1" />.
            /// </summary>
            /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only. </exception>
            public void Clear()
            {
                _resolvers.Clear();
            }

            /// <summary>
            /// Determines whether the <see cref="T:System.Collections.Generic.ICollection`1" /> contains a specific value.
            /// </summary>
            /// <returns>
            /// true if <paramref name="item" /> is found in the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false.
            /// </returns>
            /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
            public bool Contains(CustomResolver item)
            {
                return item != null && Count > 0 && _resolvers.Contains(item);
            }

            /// <summary>
            /// Copies the elements of the <see cref="T:System.Collections.Generic.ICollection`1" /> to an <see cref="T:System.Array" />, starting at a particular <see cref="T:System.Array" /> index.
            /// </summary>
            /// <param name="array">The one-dimensional <see cref="T:System.Array" /> that is the destination of the elements copied from <see cref="T:System.Collections.Generic.ICollection`1" />. The <see cref="T:System.Array" /> must have zero-based indexing.</param>
            /// <param name="arrayIndex">The zero-based index in <paramref name="array" /> at which copying begins.</param>
            /// <exception cref="T:System.ArgumentNullException"><paramref name="array" /> is null.</exception>
            /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="arrayIndex" /> is less than 0.</exception>
            /// <exception cref="T:System.ArgumentException"><paramref name="array" /> is multidimensional.-or-<paramref name="arrayIndex" /> is equal to or greater than the length of <paramref name="array" />.-or-The number of elements in the source <see cref="T:System.Collections.Generic.ICollection`1" /> is greater than the available space from <paramref name="arrayIndex" /> to the end of the destination <paramref name="array" />.-or-Type cannot be cast automatically to the type of the destination <paramref name="array" />.</exception>
            public void CopyTo(CustomResolver[] array, int arrayIndex)
            {
                _resolvers.CopyTo(array, arrayIndex);
            }

            /// <summary>
            /// Removes the first occurrence of a specific object from the <see cref="T:System.Collections.Generic.ICollection`1" />.
            /// </summary>
            /// <returns>
            /// true if <paramref name="item" /> was successfully removed from the <see cref="T:System.Collections.Generic.ICollection`1" />; otherwise, false. This method also returns false if <paramref name="item" /> is not found in the original <see cref="T:System.Collections.Generic.ICollection`1" />.
            /// </returns>
            /// <param name="item">The object to remove from the <see cref="T:System.Collections.Generic.ICollection`1" />.</param>
            /// <exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.</exception>
            public bool Remove(CustomResolver item)
            {
                return item != null && _resolvers.Remove(item);
            }

            /// <summary>
            /// Gets the number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
            /// </summary>
            /// <returns>
            /// The number of elements contained in the <see cref="T:System.Collections.Generic.ICollection`1" />.
            /// </returns>
            public int Count
            {
                get { return _resolvers.Count; }
            }

            /// <summary>
            /// Gets a value indicating whether the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only.
            /// </summary>
            /// <returns>
            /// true if the <see cref="T:System.Collections.Generic.ICollection`1" /> is read-only; otherwise, false.
            /// </returns>
            public bool IsReadOnly
            {
                get { return false; }
            }

            #endregion
        }

        #endregion

        #region Compilation

        [DebuggerStepThrough]
        private void LoadFromStream(Stream str, string filePath)
        {
#if Compression
            if(!str.CanSeek)
                goto noCompression;
            
            byte[] buffer = new byte[6];
            if(str.Read(buffer,0,6) != 6)
                goto noCompression;

            string header = System.Text.Encoding.ASCII.GetString(buffer);
            if(!header.Substring(0,5).Equals("//PXS",StringComparison.InvariantCultureIgnoreCase))
                goto noCompression;

            if(Char.ToUpperInvariant(header[5]) != 'C')
                goto noCompression;

            //Compressed
            GZipStream zip = new GZipStream(str, CompressionMode.Decompress, true);
            str = zip;

            goto compile;

            noCompression:
            str.Position -= 6;

            compile:
#endif
            var lex = new Lexer(new StreamReader(str, Encoding.UTF8));
            if (filePath != null)
            {
                lex.File = filePath;
                _loadedFiles.Add(Path.GetFullPath(filePath));
            }

            _load(lex);
        }

        [DebuggerStepThrough]
        public void LoadFromStream(Stream str)
        {
            LoadFromStream(str, null);
        }

#if DEBUG
        private int _load_indent;
#endif

        [DebuggerStepThrough]
        public void LoadFromFile(string path)
        {
            var file = ApplyLoadPaths(path);
            if (file == null)
            {
                _throwCannotFindScriptFile(path);
                return;
            }
            LoadFromFile(file);
        }

        public void LoadFromFile(FileInfo file)
        {
            if (file == null)
                throw new ArgumentNullException("file");
            _loadedFiles.Add(file.FullName);
            _loadPaths.Push(file.DirectoryName);
            using (Stream str = new FileStream(file.FullName, FileMode.Open))
            {
#if DEBUG
                var indent = new StringBuilder(_load_indent);
                indent.Append(' ', 2*(_load_indent++));
                Console.WriteLine("{1}begin compiling {0} [Path: {2} ]", file.Name, indent, file.FullName);
#endif
                LoadFromStream(str, file.Name);
#if DEBUG
                Console.WriteLine("{1}end   compiling {0}", file.Name, indent);
                _load_indent--;
#endif
            }

            _loadPaths.Pop();
        }

        public void RequireFromFile(string path)
        {
            var file = ApplyLoadPaths(path);
            if (file == null)
            {
                _throwCannotFindScriptFile(path);
                return;
            }

            if(_loadedFiles.Contains(file.FullName))
                return;

            LoadFromFile(file);
        }

        private static void _throwCannotFindScriptFile(string path)
        {
            throw new FileNotFoundException(
                "Cannot find script file \"" + path + "\".", path);
        }

        [DebuggerStepThrough]
        public void LoadFromString(string code)
        {
            _load(new Lexer(new StringReader(code)));
        }

        private Action<int, int, string> _reportSemError;

        /// <summary>
        /// Reports a semantic error to the current parsers error stream. 
        /// Can only be used while Loader is actively parsing.
        /// </summary>
        /// <param name="line">The line on which the error occurred.</param>
        /// <param name="column">The column in which the error occurred.</param>
        /// <param name="message">The error message.</param>
        /// <exception cref="InvalidOperationException">when the Loader is not actively parsing.</exception>
        public void ReportSemanticError(int line, int column, string message)
        {
            if (_reportSemError == null)
                throw new InvalidOperationException("The Loader must be parsing when this method is called.");

            _reportSemError(line, column, message);
        }

        private void _load(IScanner lexer)
        {
            var parser = new Parser(lexer, this);
            var lc = new LineCatcher();
            lc.LineCaught +=
                ((sender, o) => _errors.Add(o.Line));
            parser.errors.errorStream = lc;

            var oldReportSemError = _reportSemError;
            _reportSemError = parser.SemErr;
            parser.Parse();

            //Compile initialization function
            var target = FunctionTargets[Application.InitializationId];
            _EmitPartialInitializationCode();
            target.FinishTarget();

            _reportSemError = oldReportSemError;
        }

        [DebuggerStepThrough]
        internal void _EmitPartialInitializationCode()
        {
            var target = FunctionTargets[Application.InitializationId];
            target.ExecuteCompilerHooks();
            target.Ast.EmitCode(target, false); //do not treat initialization blocks as top-level ones.
            target.Ast.Clear();
        }

        public int ErrorCount
        {
            [DebuggerStepThrough]
            get { return _errors.Count; }
        }

        public List<string> Errors
        {
            get { return _errors; }
        }

        private readonly List<string> _errors = new List<string>();

        #endregion

        #region Load Path and file table

        private readonly Stack<string> _loadPaths = new Stack<string>();

        public Stack<string> LoadPaths
        {
            get { return _loadPaths; }
        }

        private static readonly string _imageLocation =
            (new FileInfo(Assembly.GetExecutingAssembly().Location)).DirectoryName;

        public FileInfo ApplyLoadPaths(string pathPostfix)
        {
            if (pathPostfix == null)
                throw new ArgumentNullException("pathPostfix");
            var path = pathPostfix;

            //Try to find in process environment
            if (File.Exists(path))
                return new FileInfo(path);

            //Try to find in load paths
            foreach (var pathPrefix in _loadPaths)
                if (File.Exists((path = Path.Combine(pathPrefix, pathPostfix))))
                    return new FileInfo(path);

            //Try to find in engine paths
            foreach (var pathPrefix in ParentEngine.Paths)
                if (File.Exists((path = Path.Combine(pathPrefix, pathPostfix))))
                    return new FileInfo(path);

            //Try to find in current directory
            if (File.Exists((path = Path.Combine(Environment.CurrentDirectory, pathPostfix))))
                return new FileInfo(path);

            //Try to find next to image
            if (File.Exists((path = Path.Combine(_imageLocation, pathPostfix))))
                return new FileInfo(path);

            //Not found
            return null;
        }

        private readonly SymbolCollection _loadedFiles = new SymbolCollection();

        public SymbolCollection LoadedFiles
        {
            get { return _loadedFiles; }
        }

        #endregion

        #region Build Block Commands

        private bool _buildCommandsEnabled;

        public CommandTable BuildCommands
        {
            get { return _buildCommands; }
        }

        private readonly CommandTable _buildCommands = new CommandTable();

        public bool BuildCommandsEnabled
        {
            get { return _buildCommandsEnabled; }
            set
            {
                if (value != _buildCommandsEnabled)
                    if (value)
                        _enableBuildCommands();
                    else
                        _disableBuildBlockCommands();

                _buildCommandsEnabled = value;
            }
        }

        /// <summary>
        /// The name of the add command in build blocks.
        /// </summary>
        public const string BuildAddCommand = @"Add";

        /// <summary>
        /// The name of the require command in build blocks
        /// </summary>
        public const string BuildRequireCommand = @"Require";

        /// <summary>
        /// The name of the default command in build blocks
        /// </summary>
        public const string BuildDefaultCommand = @"Default";

        /// <summary>
        /// The name of the hook command for build blocks.
        /// </summary>
        public const string BuildHookCommand = @"Hook";

        /// <summary>
        /// The name of the resolver command for build blocks.
        /// </summary>
        public const string BuildResolveCommand = "Resolve";

        /// <summary>
        /// The name of the getloader command for build blocks
        /// </summary>
        public const string BuildGetLoaderCommand = @"GetLoader";

        /// <summary>
        /// The name of the default script file
        /// </summary>
        public const string DefaultScriptName = "_default.pxs";

        private void _initializeBuildCommands()
        {
            _buildCommands.Clear();
            _buildCommands.AddCompilerCommand(
                BuildAddCommand,
                delegate(StackContext sctx, PValue[] args)
                {
                    foreach (var arg in args)
                    {
                        var path = arg.CallToString(sctx);
                        LoadFromFile(path);
                    }
                    return null;
                });

            _buildCommands.AddCompilerCommand(
                BuildRequireCommand,
                delegate(StackContext sctx, PValue[] args)
                {
                    var allLoaded = true;
                    foreach (var arg in args)
                    {
                        var path = arg.CallToString(sctx);
                        var file = ApplyLoadPaths(path);
                        if (file == null)
                        {
                            _throwCannotFindScriptFile(path);
                            return PType.Null;
                        }
                        if (_loadedFiles.Contains(file.FullName))
                            allLoaded = false;
                        else
                            LoadFromFile(file);
                    }
                    return
                        PType.Bool.CreatePValue(allLoaded);
                });

            _buildCommands.AddCompilerCommand(
                BuildDefaultCommand,
                delegate
                {
                    var defaultFile = ApplyLoadPaths(DefaultScriptName);
                    if (defaultFile == null)
                        return DefaultScriptName;
                    else
                        return defaultFile.FullName;
                });

            _buildCommands.AddCompilerCommand(
                BuildHookCommand,
                delegate(StackContext sctx, PValue[] args)
                {
                    foreach (var arg in args)
                    {
                        if (arg != null && !arg.IsNull)
                        {
                            if (arg.Type == PType.Object[typeof (AstTransformation)])
                                CompilerHooks.Add((AstTransformation) arg.Value);
                            else
                                CompilerHooks.Add(arg);
                        }
                    }
                    return PType.Null.CreatePValue();
                });

            _buildCommands.AddCompilerCommand(
                BuildResolveCommand,
                delegate(StackContext sctx, PValue[] args)
                {
                    foreach (var arg in args)
                    {
                        if (arg.Type == PType.Object[typeof (ResolveSymbol)])
                            CustomResolvers.Add(new CustomResolver((ResolveSymbol) arg.Value));
                        else
                            CustomResolvers.Add(new CustomResolver(arg));
                    }
                    return PType.Null.CreatePValue();
                });

            _buildCommands.AddCompilerCommand(
                BuildGetLoaderCommand,
                (sctx, args) => sctx.CreateNativePValue(this));
        }


        private void _enableBuildCommands()
        {
            foreach (var pair in _buildCommands)
                if (pair.Value.IsInGroup(PCommandGroups.Compiler) &&
                    ! ParentEngine.Commands.ContainsKey(pair.Key))
                    ParentEngine.Commands.AddCompilerCommand(pair.Key, pair.Value);
        }

        private void _disableBuildBlockCommands()
        {
            ParentEngine.Commands.RemoveCompilerCommands();
        }

        public void DeclareBuildBlockCommands(CompilerTarget target)
        {
            foreach (var cmdEntry in _buildCommands)
                target.Declare(SymbolInterpretations.Command, cmdEntry.Key);
        }

        #endregion

        #region Store

        public void StoreInFile(string path)
        {
#if Compress
            if(Options.Compress)
                using(FileStream fstr = new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None))
                    StoreCompressed(fstr);
            else
#endif
            using (var writer = new StreamWriter(path, false))
                Store(writer);
        }

        public string StoreInString()
        {
            var writer = new StringWriter();
            Store(writer);
            return writer.ToString();
        }

        public void Store(StringBuilder builder)
        {
            using (var writer = new StringWriter(builder))
                Store(writer);
        }

#if Compress

        public void StoreCompressed(Stream str)
        {
            if (str == null)
                throw new ArgumentNullException("str");

            using (GZipStream zip = new GZipStream(str, CompressionMode.Compress, true))
            using (StreamWriter writer = new StreamWriter(zip, Encoding.UTF8))
            {
                writer.WriteLine("//PXSC");
                Store(writer);
            }
        }

#endif

        public void Store(TextWriter writer)
        {
            var app = ParentApplication;

            //Header
            writer.WriteLine("//PXS_");
            writer.WriteLine("//--GENERATED");

            //Meta information
            StoreMetaInformation(writer);

            //Global variables
            writer.WriteLine("\n//--GLOBAL VARIABLES");
            foreach (var kvp in app.Variables)
            {
                writer.Write("var ");
                writer.Write(kvp.Key);
                var metaTable = kvp.Value.Meta.Clone();
                metaTable.Remove(Application.IdKey);
                metaTable.Remove(Application.InitializationId);
                if (metaTable.Count > 0)
                {
#if DEBUG || Verbose
                    writer.WriteLine();
#endif
                    writer.Write("[");
#if DEBUG || Verbose
                    writer.WriteLine();
#endif
                    metaTable.Store(writer);
                    writer.Write("]");
#if DEBUG || Verbose
                    writer.WriteLine();
#endif
                }
                writer.Write(";");
#if DEBUG || Verbose
                writer.WriteLine();
#endif
            }

            //Functions
            writer.WriteLine("\n//--FUNCTIONS");
            app.Functions.Store(writer);

            //add the initialization function only 
            if (app._InitializationFunction.Code.Count > 0)
                app._InitializationFunction.Store(writer);

            //Symbols
            if (Options.StoreSymbols)
                StoreSymbols(writer);
        }

        /// <summary>
        /// Writes only the symbol declarations to the text writer (regardless of the <see cref="LoaderOptions.StoreSymbols"/> property.)
        /// </summary>
        /// <param name="writer">The writer to write the declarations to.</param>
        public void StoreSymbols(TextWriter writer)
        {
            writer.WriteLine("\n//--SYMBOLS");
            var functions =
                new List<KeyValuePair<string, SymbolEntry>>();
            var commands =
                new List<KeyValuePair<string, SymbolEntry>>();
            var objectVariables =
                new List<KeyValuePair<string, SymbolEntry>>();
            var referenceVariables =
                new List<KeyValuePair<string, SymbolEntry>>();

            foreach (var kvp in Symbols)
                switch (kvp.Value.Interpretation)
                {
                    case SymbolInterpretations.Function:
                        functions.Add(kvp);
                        break;
                    case SymbolInterpretations.Command:
                        commands.Add(kvp);
                        break;
                    case SymbolInterpretations.GlobalObjectVariable:
                        objectVariables.Add(kvp);
                        break;
                    case SymbolInterpretations.GlobalReferenceVariable:
                        referenceVariables.Add(kvp);
                        break;
                }

            _writeSymbolKind(writer, "function", functions);
            _writeSymbolKind(writer, "command", commands);
            _writeSymbolKind(writer, "var", objectVariables);
            _writeSymbolKind(writer, "ref", referenceVariables);
        }

        /// <summary>
        /// Writes only meta information to the specified stream.
        /// </summary>
        /// <param name="writer">The writer to write meta information to.</param>
        public void StoreMetaInformation(TextWriter writer)
        {
            writer.WriteLine("\n//--META INFORMATION");
            ParentApplication.Meta.Store(writer);
        }

        private static void _writeSymbolKind(
            TextWriter writer,
            string kind,
            ICollection<KeyValuePair<string, SymbolEntry>> entries)
        {
            if (entries.Count <= 0)
                return;
            writer.Write("declare ");
            writer.Write(kind);
            writer.Write(" ");
            var idx = 0;
            foreach (var kvp in entries)
            {
                writer.Write(kvp.Value.Id);
                if (!Engine.StringsAreEqual(kvp.Value.Id, kvp.Key))
                {
                    writer.Write(" as ");
                    writer.Write(kvp.Key);
                }
                if (++idx == entries.Count)
                    writer.WriteLine(";");
                else
                    writer.Write(",");
            }
        }

        #endregion

        #region Stack Context

        public override sealed Engine ParentEngine
        {
            [DebuggerStepThrough]
            get { return _options.ParentEngine; }
        }

        public PFunction Implementation
        {
            [DebuggerStepThrough]
            get { return Options.TargetApplication._InitializationFunction; }
        }

        public override sealed Application ParentApplication
        {
            get { return Options.TargetApplication; }
        }

        public override sealed SymbolCollection ImportedNamespaces
        {
            get { return Options.TargetApplication._InitializationFunction.ImportedNamespaces; }
        }

        [DebuggerStepThrough]
        protected override bool PerformNextCylce(StackContext lastContext)
        {
            return false;
        }

        public override PValue ReturnValue
        {
            [DebuggerStepThrough]
            get { return Options.ParentEngine.CreateNativePValue(Options.TargetApplication); }
        }

        public override bool TryHandleException(Exception exc)
        {
            //Cannot handle exceptions.
            return false;
        }

        #endregion

        #region String Caching

        private readonly Dictionary<string, string> _stringCache = new Dictionary<string, string>();

        /// <summary>
        /// Caches strings encountered while loading code.
        /// </summary>
        /// <param name="toCache">The string to cache.</param>
        /// <returns>The cached instance of the supplied string. </returns>
        [DebuggerStepThrough]
        public string CacheString(string toCache)
        {
            string cached;
            if(_stringCache.TryGetValue(toCache, out cached))
            {
                return cached;
            }
            else
            {
                cached = toCache;
                _stringCache.Add(toCache, cached);
                return cached;
            }
        }

        #endregion

        public const string CilHintsKey = "cilhints";

    }
}