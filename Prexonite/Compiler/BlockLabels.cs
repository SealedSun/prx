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
using NoDebug = System.Diagnostics.DebuggerNonUserCodeAttribute;

namespace Prexonite.Compiler
{
    [NoDebug]
    public class BlockLabels
    {
        private readonly string _continueLabel;

        public string ContinueLabel
        {
            get { return _continueLabel; }
        }

        private readonly string _breakLabel;

        public string BreakLabel
        {
            get { return _breakLabel; }
        }

        private readonly string _beginLabel;

        public string BeginLabel
        {
            get { return _beginLabel; }
        }

        private readonly string _prefix;

        public string Prefix
        {
            get { return _prefix.Substring(0, _prefix.Length - 1); }
        }

        private readonly string _uid;

        public string Uid
        {
            get { return _uid; }
        }

        public const string ContinueWord = "continue";
        public const string BreakWord = "break";
        public const string BeginWord = "begin";
        public const string DefaultPrefix = "_";

        public BlockLabels(string prefix)
            : this(prefix, "\\" + Guid.NewGuid().ToString("N"))
        {
        }

        private BlockLabels(string prefix, string uid)
        {
            if (String.IsNullOrEmpty(prefix))
                prefix = DefaultPrefix;
            _prefix = prefix + "\\";
            _uid = uid;
            _continueLabel = CreateLabel(ContinueWord);
            _breakLabel = CreateLabel(BreakWord);
            _beginLabel = CreateLabel(BeginLabel);
        }

        public static BlockLabels CreateExistingLabels(string prefix, string uid)
        {
            return new BlockLabels(prefix, uid);
        }

        public string CreateLabel(string verb)
        {
            return String.Concat(_prefix, verb, _uid);
        }

        public BlockLabels()
            : this(null)
        {
        }
    }
}