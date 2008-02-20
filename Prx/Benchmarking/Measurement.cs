using System;
using System.Diagnostics;

namespace Prx.Benchmarking
{
    [DebuggerStepThrough]
    public sealed class Measurement
    {

        public Measurement(BenchmarkEntry parentEntry, long rawMilliseconds, long overheadMilliseconds)
        {
            if (parentEntry == null)
                throw new ArgumentNullException("parentEntry");
            _entry = parentEntry;
            _rawMilliseconds = rawMilliseconds;
            _overheadMilliseconds = overheadMilliseconds;
            _entry.Measurements.Add(this);
        }

        public BenchmarkEntry Entry
        {
            get { return _entry; }
        }
        private readonly BenchmarkEntry _entry;
        private readonly long _overheadMilliseconds;
        private readonly long _rawMilliseconds;

        public double RawSeconds
        {
            get
            {
                return _rawMilliseconds / 1000.0;
            }
        }

        public double PassMilliseconds
        {
            get
            {
                return _rawMilliseconds / (double)Entry.Parent.Iterations;
            }
        }

        public double PassMicroseconds
        {
            get
            {
                return checked(PassMilliseconds * 1000);
            }
        }

        public long ClearedMilliseconds
        {
            get
            {
                return (_rawMilliseconds - _overheadMilliseconds);
            }
        }

        public long ClearedMicroseconds
        {
            get
            {
                return checked(ClearedMilliseconds * 1000);
            }
        }

        public double ClearedPassMilliseconds
        {
            get
            {
                return ClearedMilliseconds/(double)Entry.Parent.Iterations;
            }
        }

        public double ClearedPassMicroseconds
        {
            get
            {
                return checked(ClearedPassMilliseconds*1000);
            }
        }

        public long OverheadMilliseconds
        {
            get
            {
                return _overheadMilliseconds;
            }
        }

        public long RawMilliseconds
        {
            get { return _rawMilliseconds; }
        }
    }
}
