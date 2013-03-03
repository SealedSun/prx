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
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Prexonite;
using Prexonite.Compiler.Cil;
using Prexonite.Modular;

namespace PrexoniteTests.Tests
{
    [TestFixture]
    public class CilRuntime
    {
        [Test]
        public void RuntimeMethodsLinked()
        {
            var rt = typeof (Runtime);
            var cs = from m in rt.GetMembers(BindingFlags.Static | BindingFlags.Public)
                     where m.Name.EndsWith("PrepareTargets") && m is PropertyInfo || m is FieldInfo
                     let v = _invokeStatic(m) 
                     select Tuple.Create(m,v);

            foreach (var t in cs)
                Assert.That(t.Item2, Is.Not.Null,
                    string.Format("The field/property Runtime.{0} is null.", t.Item1.Name));
        }

        private Object _invokeStatic(MemberInfo m)
        {
            if(m is PropertyInfo)
            {
                var p = (PropertyInfo) m;
                return p.GetValue(null, new object[0]);
            }
            else if(m is FieldInfo)
            {
                var f = (FieldInfo) m;
                return f.GetValue(null);
            }
            else
            {
                var message = string.Format("The member {1}.{0} is not a property or field.", m.Name, m.DeclaringType);
                Assert.Fail(message);
// ReSharper disable HeuristicUnreachableCode
                throw new Exception(message);
// ReSharper restore HeuristicUnreachableCode
            }
        }
    }
}