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
using System.IO;
using System.Text;

namespace Prexonite.Compiler.Build.Internal
{
    public class FileSource : ISource
    {
        private readonly FileInfo _file;
        private readonly Encoding _encoding;

        public FileSource(FileInfo file, Encoding encoding)
        {
            if ((object) file == null)
                throw new System.ArgumentNullException("file");
            if ((object) encoding == null)
                throw new ArgumentNullException("encoding");
            _file = file;
            _encoding = encoding;
        }

        #region Implementation of ISource

        public bool CanOpen
        {
            get { return _file.Exists; }
        }

        public bool IsSingleUse
        {
            get { return false; }
        }

        public bool TryOpen(out TextReader reader)
        {
            if(!_file.Exists)
            {
                reader = null;
                return false;
            }

            try
            {
                reader = new StreamReader(_file.FullName,_encoding);
                return true;
            }
            catch (FileNotFoundException)
            {
                reader = null;
                return false;
            }
        }

        #endregion
    }
}