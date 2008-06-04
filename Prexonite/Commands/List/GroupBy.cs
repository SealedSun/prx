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
using System.Collections.Generic;
using System.Text;
using Prexonite.Types;

namespace Prexonite.Commands.List
{
    public class GroupBy : CoroutineCommand
    {
        protected override IEnumerable<PValue> CoroutineRun(StackContext sctx, PValue[] args)
        {
            if (args == null)
                throw new ArgumentNullException("args");
            if (sctx == null)
                throw new ArgumentNullException("sctx");

            if (args.Length < 1)
                throw new PrexoniteException("GroupBy requires at least one argument.");

            PValue f = args[0];

            Dictionary<PValue, List<PValue>> groups =
                new Dictionary<PValue, List<PValue>>();

            for (int i = 1; i < args.Length; i++)
            {
                PValue arg = args[i];
                IEnumerable<PValue> xs = Map._ToEnumerable(sctx, arg);
                if(xs == null)
                    continue;
                foreach (PValue x in xs)
                {
                    PValue fx = f.IndirectCall(sctx, new PValue[] {x});
                    if (!groups.ContainsKey(fx))
                    {
                        List<PValue> lst = new List<PValue>();
                        lst.Add(x);
                        groups.Add(fx, lst);
                    }
                    else
                    {
                        groups[fx].Add(x);
                    }
                }
            }

            foreach (KeyValuePair<PValue, List<PValue>> pair in groups)
                yield return new PValueKeyValuePair(pair.Key, (PValue)pair.Value);
        }

        /// <summary>
        /// A flag indicating whether the command acts like a pure function.
        /// </summary>
        /// <remarks>Pure commands can be applied at compile time.</remarks>
        public override bool IsPure
        {
            get { return false; }
        }
    }
}