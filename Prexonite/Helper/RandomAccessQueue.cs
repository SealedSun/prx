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
using System.Collections;
using System.Collections.Generic;
using NoDebug = System.Diagnostics.DebuggerNonUserCodeAttribute;

namespace Prexonite.Helper
{
    /// <summary>
    /// Custom implementation of a queue that allows random access.
    /// </summary>
    /// <typeparam name="T">The type of the elements the queue is supposed to manage.</typeparam>
    public class RandomAccessQueue<T> : IList<T>
    {
        #region Constructors

        private const int DEFAULT_INITIAL_CAPACITY = 10;

        /// <summary>
        /// Creates a new RandomAccessQueue
        /// </summary>
        /// <remarks>This overload uses a default value for the capacity of it's data store.</remarks>
        [NoDebug]
        public RandomAccessQueue()
        {
            _store = new List<T>();
        }

        /// <summary>
        /// Creates a new RandomAccessQueue.
        /// </summary>
        /// <param name="collection">Elements to copy to the queue upon creation.</param>
        [NoDebug]
        public RandomAccessQueue(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            _store = new List<T>(collection);
        }

        /// <summary>
        /// Creates a new RandomAccessQueue
        /// </summary>
        /// <param name="capacity">The initial capacity of the queue.</param>
        /// <remarks>Although the queue increases the size of it's data store as required, setting an 
        /// initial capacity can reduce the number of resize operations, when filling the queue.</remarks>
        [NoDebug]
        public RandomAccessQueue(int capacity)
        {
            _store = new List<T>(capacity);
        }

        #endregion

        #region Core

        private List<T> _store;
        private int _front = 0;
        private int _rear = -1;

        private void unwrap()
        {
            T[] nstore = new T[_store.Count];
            int count;
            count = normalCount();
            _store.CopyTo(_front, nstore, 0, count);

            //Copy the wrapped part, if necessary
            if (isWrapped())
            {
                int wrapped = wrappedCount();
                _store.CopyTo(0, nstore, count, wrapped);
            }

            //Write queue back to the store
            _store.Clear();
            _store.Capacity = nstore.Length;
            _front = 0;
            _rear = -1;
            foreach (T t in nstore)
            {
                _store.Add(t);
                _rear++;
            }
        }

        private int normalCount()
        {
            if (_rear < 0)
                return 0;
            else if (isWrapped())
                return _store.Count - _front;
            else
                return _rear + 1 - _front;
        }

        private int wrappedCount()
        {
            return isWrapped() ? _rear + 1 - 0 : 0;
        }

        [NoDebug]
        private bool isWrapped()
        {
            return _front > _rear;
        }

        private int toIndex(int qidx)
        {
            int idx = _front + qidx;
            if (idx >= _store.Count)
                idx -= _store.Count;
            return idx;
        }

        #endregion

        #region Queue Members

        /// <summary>
        /// Adds an element to the end of the queue.
        /// </summary>
        /// <param name="item">The element to be added to the end of the queue.</param>
        public void Enqueue(T item)
        {
            if (_rear == -1)
            {
                if (_store.Count >= 1)
                    _store[0] = item;
                else
                    _store.Add(item);
                _rear++;
            }
            else if (!isWrapped())
                if (_rear + 1 < _store.Count || _store.Count < DEFAULT_INITIAL_CAPACITY)
                {
                    //Stay unwrapped
                    if (++_rear == _store.Count)
                        _store.Add(item);
                    else
                        _store[_rear] = item;
                }
                else //Wrap!
                    if (_front == 0) //no space. resize instead.
                    {
                        _store.Add(item);
                        _rear++;
                    }
                    else
                    {
                        _rear = 0;
                        _store[0] = item;
                    }
            else //wrapped
                if (_rear + 1 == _front)
                {
                    int newRear = Count - 1 + 1;
                    unwrap();
                    _store.Add(item);
                    _rear = newRear;
                }
                else
                {
                    _rear++;
                    _store[_rear] = item;
                }
        }

        /// <summary>
        /// Returns the element in front of the queue (to be dequeued next).
        /// </summary>
        /// <returns>The element in front of the queue (to be dequeued next).</returns>
        [NoDebug]
        public T Peek()
        {
            return _store[_front];
        }

        /// <summary>
        /// Removes and returns the element in front of the queue.
        /// </summary>
        /// <returns>The element in front of the queue.</returns>
        public T Dequeue()
        {
            T item = _store[_front];
            _store[_front] = default(T); //Make sure, item get's garbage collected
            if (_front == _rear) //just removed last element -> reset
            {
                _front = 0;
                _rear = -1;
            }
            else if (_front + 1 >= _store.Count)
                _front = 0;
            else
                _front++;
            return item;
        }

        #endregion

        #region IList<T> Members

        /// <summary>
        /// Returns the index at which <paramref name="item"/> is located.
        /// </summary>
        /// <param name="item">The item to search for.</param>
        /// <returns>The index in the queue where the item is stored or -1 if the item cannot be found.</returns>
        [NoDebug]
        public int IndexOf(T item)
        {
            int normal = _store.IndexOf(
                item,
                _front,
                normalCount());
            if (normal < 0 && isWrapped())
                return _store.IndexOf(item, 0, wrappedCount());
            else
                return normal;
        }

        /// <summary>
        /// Inserts an item at a random position in the queue.
        /// </summary>
        /// <param name="index">Where to insert <paramref name="item"/>.</param>
        /// <param name="item">What to insert at <paramref name="index"/>.</param>
        [NoDebug]
        public void Insert(int index, T item)
        {
            _store.Insert(toIndex(index), item);
        }

        /// <summary>
        /// Removes the element at a supplied index.
        /// </summary>
        /// <param name="index">The index of the element to remove.</param>
        [NoDebug]
        public void RemoveAt(int index)
        {
            _store.RemoveAt(toIndex(index));
        }

        /// <summary>
        /// Random access to the queue.
        /// </summary>
        /// <param name="index">The index of the element to retrieve or set.</param>
        /// <returns>The element at the supplied index.</returns>
        public T this[int index]
        {
            [NoDebug]
            get { return _store[toIndex(index)]; }
            [NoDebug]
            set { _store[toIndex(index)] = value; }
        }

        #endregion

        #region ICollection<T> Members

        /// <summary>
        /// Adds an element to the queue. Synonym to <see cref="Enqueue"/>.
        /// </summary>
        /// <param name="item">The item to add (enqueue).</param>
        [NoDebug]
        public void Add(T item)
        {
            Enqueue(item);
        }

        /// <summary>
        /// Removes all elements from the queue.
        /// </summary>
        [NoDebug]
        public void Clear()
        {
            _store.Clear();
            _front = 0;
            _rear = 0;
        }

        /// <summary>
        /// Indicates whether the queue contains a specific element.
        /// </summary>
        /// <param name="item">The element to look for.</param>
        /// <returns>True, if the queue contains the element; false otherwise.</returns>
        [NoDebug]
        public bool Contains(T item)
        {
            return IndexOf(item) >= 0;
        }

        /// <summary>
        /// Copies the contents of the queue to the specified array.
        /// </summary>
        /// <param name="array">An array, that is big enough to hold all elements in the queue.</param>
        /// <param name="arrayIndex">The index in the supplied array, that indicates where to start writing.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="array"/> is not big enough.</exception>
        [NoDebug]
        public void CopyTo(T[] array, int arrayIndex)
        {
            int idx = arrayIndex;
            if (array.Length < Count)
                throw new ArgumentOutOfRangeException(
                    "The supplied array is not big enough for " + Count + " elements.");

            foreach (T t in this)
                array[idx++] = t;
        }

        /// <summary>
        /// Retuns the number of elements in the queue
        /// </summary>
        public int Count
        {
            get { return normalCount() + wrappedCount(); }
        }

        /// <summary>
        /// Queues are never readonly. Always returns false.
        /// </summary>
        public bool IsReadOnly
        {
            [NoDebug]
            get { return false; }
        }

        /// <summary>
        /// Removes an element from the queue.
        /// </summary>
        /// <param name="item">The element to remove.</param>
        /// <returns>True if an element has been removed; false otherwise.</returns>
        [NoDebug]
        public bool Remove(T item)
        {
            int idx = IndexOf(item);
            if (idx < 0)
                return false;
            else
                _store.Remove(item);
            return true;
        }

        #endregion

        #region IEnumerable<T> Members

        /// <summary>
        /// Returns an IEnumerator that enumerates over all elements in the queue.
        /// </summary>
        /// <returns></returns>
        [NoDebug]
        public IEnumerator<T> GetEnumerator()
        {
            int count = normalCount();

            //Return normal part.
            for (int i = _front; i < count; i++)
                yield return _store[i];

            //Check if there exists a wrapped part
            if (isWrapped())
            {
                int wrapped = wrappedCount();

                //Return the wrapped part.
                for (int i = 0; i < wrapped; i++)
                    yield return _store[i];
            }
        }

        #endregion

        #region IEnumerable Members

        [NoDebug]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}